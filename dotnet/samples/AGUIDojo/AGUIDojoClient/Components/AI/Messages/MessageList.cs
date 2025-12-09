// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.AI;

#pragma warning disable CA1812 // Internal class is apparently never instantiated
internal sealed partial class MessageList : IComponent, IDisposable
#pragma warning restore CA1812 // Internal class is apparently never instantiated
{
    private RenderHandle _renderHandle;
    private MessageSubscription _subscription;

    [CascadingParameter] public MessageListContext MessageListContext { get; set; } = default!;

    public void Attach(RenderHandle renderHandle)
    {
        this._renderHandle = renderHandle;
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        var previousContext = this.MessageListContext;
        parameters.SetParameterProperties(this);

        if (previousContext != null && this.MessageListContext != previousContext)
        {
            throw new InvalidOperationException(
                $"{nameof(MessageList)} does not support changing the {nameof(this.MessageListContext)} once it has been set.");
        }

        // Subscribe to message updates
        this._subscription = this.MessageListContext.AgentBoundaryContext.SubscribeToMessageChanges(this.RenderOnNewMessage);
        Log.MessageListAttached(this.MessageListContext.AgentBoundaryContext.Logger);
        Log.MessageListSubscribed(this.MessageListContext.AgentBoundaryContext.Logger);

        // Initial render. This component will only render once since the only parameter is a cascading parameter and it's fixed.
        this._renderHandle.Render(this.Render);

        return Task.CompletedTask;
    }

    private void RenderOnNewMessage() => this._renderHandle.Render(this.Render);

    public void Render(RenderTreeBuilder builder)
    {
        Log.MessageListRendering(
            this.MessageListContext.AgentBoundaryContext.Logger,
            this.MessageListContext.AgentBoundaryContext.CompletedMessages.Count,
            this.MessageListContext.AgentBoundaryContext.PendingMessages.Count);

        foreach (var message in this.MessageListContext.AgentBoundaryContext.CompletedMessages)
        {
            // Calling GetTemplate will stop template collection on the first message if it
            // was still ongoing.
            builder.OpenComponent<MessageRenderer>(0);
            builder.SetKey(message.MessageId);
            builder.AddComponentParameter(1, "ChildContent", this.MessageListContext.GetTemplate(message));
            builder.CloseComponent();
        }

        foreach (var message in this.MessageListContext.AgentBoundaryContext.PendingMessages)
        {
            builder.OpenComponent<MessageRenderer>(1);
            builder.SetKey(message.MessageId);
            builder.AddComponentParameter(1, "ChildContent", this.MessageListContext.GetTemplate(message));
            builder.CloseComponent();
        }
    }

    public void Dispose()
    {
        Log.MessageListDisposed(this.MessageListContext.AgentBoundaryContext.Logger);
        ((IDisposable)this._subscription).Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "MessageList attached to render handle")]
        public static partial void MessageListAttached(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "MessageList subscribed to message changes")]
        public static partial void MessageListSubscribed(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "MessageList rendering, completed: {CompletedCount}, pending: {PendingCount}")]
        public static partial void MessageListRendering(ILogger logger, int completedCount, int pendingCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "MessageList disposed")]
        public static partial void MessageListDisposed(ILogger logger);
    }
}
