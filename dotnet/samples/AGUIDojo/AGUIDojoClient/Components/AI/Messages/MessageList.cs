// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.AI;

#pragma warning disable CA1812 // Internal class is apparently never instantiated
internal sealed class MessageList : IComponent, IDisposable
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

        // Initial render. This component will only render once since the only parameter is a cascading parameter and it's fixed.
        this._renderHandle.Render(this.Render);

        return Task.CompletedTask;
    }

    private void RenderOnNewMessage() => this._renderHandle.Render(this.Render);

    public void Render(RenderTreeBuilder builder)
    {
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
        ((IDisposable)this._subscription).Dispose();
    }
}
