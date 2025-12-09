// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Microsoft.AspNetCore.Components.AI;

public class AgentBoundaryContext<TState> : IAgentBoundaryContext, IDisposable
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<ChatMessage> _messages = [];
    private readonly List<ChatMessage> _pendingMessages = [];
    private readonly List<Action> _messageChangeSubscribers = [];
    private readonly List<Action> _responseUpdateSubscribers = [];

    public CancellationToken CancellationToken => this._cancellationTokenSource.Token;

    public IReadOnlyList<ChatMessage> CompletedMessages => this._messages.AsReadOnly();

    public TState? CurrentState { get; set; }

    public IReadOnlyList<ChatMessage> PendingMessages => this._pendingMessages.AsReadOnly();

    public ChatMessage? CurrentMessage { get; private set; }

    public ChatResponseUpdate? CurrentUpdate { get; private set; }

    public AgentBoundaryContext(AIAgent agent, AgentThread thread)
    {
        this._agent = agent;
        this._thread = thread;
        this._cancellationTokenSource = new CancellationTokenSource();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._cancellationTokenSource.Cancel();
            this._cancellationTokenSource.Dispose();
        }
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task SendAsync(params ChatMessage[] userMessages)
    {
        // This starts a new turn. Once completed, we will add the new messages to the conversation.
        this._pendingMessages.Clear();
        this._pendingMessages.AddRange(userMessages);

        // User messages added, notify subscribers.
        this.TriggerMessageChanges();

        // Start a turn. Collect all updates as we stream them.
        await foreach (var update in this._agent.RunStreamingAsync(
            userMessages,
            this._thread,
            options: null,
            this._cancellationTokenSource.Token))
        {
            var chatUpdate = update.AsChatResponseUpdate();

            this.CurrentUpdate = chatUpdate;

            // Notify subscribers of the new update, this always happens before a new message is created/updated.
            this.TriggerChatResponseUpdate();

            // This creates or adds a new message to the pending messages as needed.
            var messageCountBefore = this._pendingMessages.Count;
            var isNewMessage = MessageHelpers.ProcessUpdate(chatUpdate, this._pendingMessages);
            if (isNewMessage)
            {
                this.CurrentMessage = this._pendingMessages[this._pendingMessages.Count - 1];

                // Finalize the previous message's content if we have 2 or more messages now.
                if (this._pendingMessages.Count > userMessages.Length + 1)
                {
                    MessageHelpers.CoalesceContent(this._messages[this._messages.Count - 2].Contents);
                }

                // Notify subscribers of new message
                this.TriggerMessageChanges();
            }
        }

        // Process any remaining updates to finalize the last message.
        if (this._pendingMessages.Count > userMessages.Length)
        {
            MessageHelpers.CoalesceContent(this._pendingMessages[this._pendingMessages.Count - 1].Contents);
        }

        // Add the new messages to the conversation
        this._messages.AddRange(this._pendingMessages);

        // Finish the turn
        this._pendingMessages.Clear();
        this.CurrentMessage = null;
        this.CurrentUpdate = null;

        // Notify subscribers
        this.TriggerChatResponseUpdate();
        this.TriggerMessageChanges();
    }

    private void TriggerChatResponseUpdate()
    {
        foreach (var subscriber in this._responseUpdateSubscribers)
        {
            subscriber();
        }
    }

    private void TriggerMessageChanges()
    {
        foreach (var subscriber in this._messageChangeSubscribers)
        {
            subscriber();
        }
    }

    public MessageSubscription SubscribeToMessageChanges(Action onNewMessage)
    {
        return new MessageSubscription(this._messageChangeSubscribers, onNewMessage);
    }

    public ResponseUpdateSubscription SubscribeToResponseUpdates(Action onChatResponse)
    {
        return new ResponseUpdateSubscription(this._responseUpdateSubscribers, onChatResponse);
    }

    ~AgentBoundaryContext()
    {
        this.Dispose(false);
    }
}

public interface IAgentBoundaryContext
{
    // Push new messages to the agent
    Task SendAsync(params ChatMessage[] userMessages);

    // All message interactions from previous turns
    IReadOnlyList<ChatMessage> CompletedMessages { get; }

    // All message interactions from the current turn. These represent completed messages only.
    IReadOnlyList<ChatMessage> PendingMessages { get; }

    // The current message being processed by the agent.
    ChatMessage? CurrentMessage { get; }

    ChatResponseUpdate? CurrentUpdate { get; }

    // Triggered any time there is a change on a message.
    MessageSubscription SubscribeToMessageChanges(Action onNewMessage);

    ResponseUpdateSubscription SubscribeToResponseUpdates(Action onChatResponse);

    CancellationToken CancellationToken { get; }
}

public readonly struct MessageSubscription : IDisposable, IEquatable<MessageSubscription>
{
    internal readonly List<Action> _subscribers;
    internal readonly Action _subscription;

    internal MessageSubscription(List<Action> subscribers, Action subscription)
    {
        this._subscribers = subscribers;
        this._subscription = subscription;
        this._subscribers.Add(this._subscription);
    }

    public void Dispose() => this._subscribers.Remove(this._subscription);

    public override bool Equals(object? obj)
    {
        return obj is MessageSubscription subscription && this.Equals(subscription);
    }

    public bool Equals(MessageSubscription other)
    {
        return EqualityComparer<List<Action>>.Default.Equals(this._subscribers, other._subscribers) &&
               EqualityComparer<Action>.Default.Equals(this._subscription, other._subscription);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this._subscribers, this._subscription);
    }

    public static bool operator ==(MessageSubscription left, MessageSubscription right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MessageSubscription left, MessageSubscription right)
    {
        return !(left == right);
    }
}

public readonly struct ResponseUpdateSubscription : IDisposable, IEquatable<ResponseUpdateSubscription>
{
    private readonly List<Action> _subscribers;
    private readonly Action _subscription;

    public ResponseUpdateSubscription(List<Action> subscribers, Action subscription)
    {
        this._subscribers = subscribers;
        this._subscription = subscription;
    }

    public void Dispose() => this._subscribers.Remove(this._subscription);

    public override bool Equals(object? obj)
    {
        return obj is ResponseUpdateSubscription subscription && this.Equals(subscription);
    }

    public bool Equals(ResponseUpdateSubscription other)
    {
        return EqualityComparer<List<Action>>.Default.Equals(this._subscribers, other._subscribers) &&
               EqualityComparer<Action>.Default.Equals(this._subscription, other._subscription);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this._subscribers, this._subscription);
    }

    public static bool operator ==(ResponseUpdateSubscription left, ResponseUpdateSubscription right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ResponseUpdateSubscription left, ResponseUpdateSubscription right)
    {
        return !(left == right);
    }
}
