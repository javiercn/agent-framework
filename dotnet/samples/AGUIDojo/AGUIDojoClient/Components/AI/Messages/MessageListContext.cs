// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace Microsoft.AspNetCore.Components.AI;

internal sealed partial class MessageListContext
{
    private bool _collectingTemplates;

    private readonly List<MessageTemplateBase> _templates = [];
    private readonly List<ContentTemplateBase> _contentTemplates = [];

    // We compute a render fragment to render each message only once and cache it here.
    private readonly Dictionary<ChatMessage, RenderFragment> _templateCache = [];

    public MessageListContext(IAgentBoundaryContext context)
    {
        this.AgentBoundaryContext = context;
        Log.MessageListContextCreated(this.AgentBoundaryContext.Logger);
    }

    public IAgentBoundaryContext AgentBoundaryContext { get; }

    public void BeginCollectingTemplates()
    {
        // This is triggered by the Messages component before rendering its children.
        // In this situation we are going to render again the MessageTemplates and
        // ContentTemplates and since we can't tell if they have changed we have to
        // recompute all the templates again.
        this._collectingTemplates = true;
        this._templates.Clear();
        this._contentTemplates.Clear();
        this._templateCache.Clear();
        Log.BeganCollectingTemplates(this.AgentBoundaryContext.Logger);
    }

    public void RegisterTemplate(MessageTemplateBase template)
    {
        if (this._collectingTemplates)
        {
            this._templates.Add(template);
            Log.MessageTemplateRegistered(this.AgentBoundaryContext.Logger, this._templates.Count);
        }
    }

    public void RegisterContentTemplate(ContentTemplateBase template)
    {
        if (this._collectingTemplates)
        {
            this._contentTemplates.Add(template);
            Log.ContentTemplateRegistered(this.AgentBoundaryContext.Logger, this._contentTemplates.Count);
        }
    }

    internal RenderFragment GetTemplate(ChatMessage message)
    {
        // We are about to render the first message. If we were collecting templates, stop now.
        this._collectingTemplates = false;
        if (this._templateCache.TryGetValue(message, out var cachedTemplate))
        {
            return cachedTemplate;
        }

        var messageContext = new MessageContext(message, this);
        foreach (var template in this._templates)
        {
            if (template.When(messageContext))
            {
                var chosen = template;
                messageContext.SetTemplate(chosen);
                // We ask the template to create a RenderFragment for the message.
                // The template will render a wrapper and use the messageContext to
                // render the contents.
                // The template might call back through the messageContext to get renderers for
                // contents if the message template doesn't override the full rendering or
                // if it doesn't define the rendering for a content type.
                var renderer = chosen.ChildContent(messageContext);
                this._templateCache[message] = renderer;
                Log.TemplateResolved(this.AgentBoundaryContext.Logger, message.Role.Value);
                return renderer;
            }
        }

        throw new InvalidOperationException($"No message template found for message of type {message.Role}.");
    }

    internal RenderFragment GetContentTemplate(AIContent content)
    {
        foreach (var template in this._contentTemplates)
        {
            var contentContext = new ContentContext(content);
            if (template.When(contentContext))
            {
                return template.ChildContent(contentContext);
            }
        }

        throw new InvalidOperationException($"No content template found for content of type {content.GetType().Name}.");
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "MessageListContext created")]
        public static partial void MessageListContextCreated(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "MessageListContext began collecting templates")]
        public static partial void BeganCollectingTemplates(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Message template registered, total: {TemplateCount}")]
        public static partial void MessageTemplateRegistered(ILogger logger, int templateCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Content template registered, total: {TemplateCount}")]
        public static partial void ContentTemplateRegistered(ILogger logger, int templateCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Template resolved for message with role: {Role}")]
        public static partial void TemplateResolved(ILogger logger, string role);
    }
}
