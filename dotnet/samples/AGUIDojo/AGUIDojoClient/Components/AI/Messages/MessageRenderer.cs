// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.AI;

#pragma warning disable CA1812 // Internal class is apparently never instantiated
internal sealed class MessageRenderer : IComponent
#pragma warning restore CA1812 // Internal class is apparently never instantiated
{
    private RenderHandle _renderHandle;

    [Parameter] public RenderFragment ChildContent { get; set; } = default!;

    public void Attach(RenderHandle renderHandle)
    {
        this._renderHandle = renderHandle;
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        var childContent = this.ChildContent;
        parameters.SetParameterProperties(this);
        if (!ReferenceEquals(childContent?.Target, this.ChildContent.Target) ||
           !ReferenceEquals(childContent?.Method, this.ChildContent.Method))
        {
            this._renderHandle.Render(this.Render);
        }
        return Task.CompletedTask;
    }

    private void Render(RenderTreeBuilder builder)
    {
        builder.AddContent(0, this.ChildContent);
    }
}
