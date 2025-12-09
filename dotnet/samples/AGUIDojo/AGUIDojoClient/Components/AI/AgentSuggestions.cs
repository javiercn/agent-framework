// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace Microsoft.AspNetCore.Components.AI;

public partial class AgentSuggestions : IComponent
{
    private RenderHandle _renderHandle;
    private AgentBoundaryContext<object?>? _context;
    private IReadOnlyList<string>? _suggestions;

    [CascadingParameter] public AgentBoundaryContext<object?>? AgentContext { get; set; }

    [Parameter] public IReadOnlyList<string>? Suggestions { get; set; }

    public void Attach(RenderHandle renderHandle)
    {
        this._renderHandle = renderHandle;
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this._context = this.AgentContext;
        this._suggestions = this.Suggestions;
        this.Render();
        return Task.CompletedTask;
    }

    private void Render()
    {
        this._renderHandle.Render(builder =>
        {
            if (this._suggestions is null || this._suggestions.Count == 0)
            {
                return;
            }

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "agent-suggestions");

            for (var i = 0; i < this._suggestions.Count; i++)
            {
                var suggestion = this._suggestions[i];
                builder.OpenElement(2, "button");
                builder.SetKey(suggestion);
                builder.AddAttribute(3, "class", "suggestion-button");
                builder.AddAttribute(4, "onclick", EventCallback.Factory.Create(this, () => this.SelectSuggestionAsync(suggestion)));
                builder.AddContent(5, suggestion);
                builder.CloseElement();
            }

            builder.CloseElement();
        });
    }

    private async Task SelectSuggestionAsync(string suggestion)
    {
        if (this._context != null)
        {
            await this._context.SendAsync(new ChatMessage(ChatRole.User, suggestion));
        }
    }
}
