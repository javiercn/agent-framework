// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Microsoft.AspNetCore.Components.AI;

/// <summary>
/// Template for rendering function/tool result content in messages.
/// </summary>
public class FunctionResultTemplate : ContentTemplateBase
{
    /// <summary>
    /// Gets or sets the tool name to filter on. If null, matches all function results.
    /// </summary>
    [Parameter] public string? ToolName { get; set; }

    [CascadingParameter] internal MessageListContext Context { get; set; } = default!;

    public override void Attach(RenderHandle renderHandle)
    {
        // This component never renders anything by itself.
        this.ChildContent = this.RenderFunctionResult;
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.Context.RegisterContentTemplate(this);
        return Task.CompletedTask;
    }

    public new bool When(ContentContext context)
    {
        if (context.Content is not FunctionResultContent)
        {
            return false;
        }

        // FunctionResultContent doesn't have a Name property, so we can't filter by tool name
        // For now, match all function results. Tool name filtering would require correlating
        // with FunctionCallContent via CallId.
        return true;
    }

    private RenderFragment RenderFunctionResult(ContentContext content) => builder =>
    {
        if (content.Content is FunctionResultContent functionResult)
        {
            // Create a context that provides access to the result
            var resultContext = new FunctionResultContext(functionResult);
            builder.OpenComponent<CascadingValue<FunctionResultContext>>(0);
            builder.AddComponentParameter(1, "Value", resultContext);
            builder.AddComponentParameter(2, "IsFixed", true);
            builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(innerBuilder =>
            {
                // Render the call ID as a fallback
                innerBuilder.AddContent(0, functionResult.CallId ?? "Function Result");
            }));
            builder.CloseComponent();
        }
    };
}

/// <summary>
/// Context for function result content rendering.
/// </summary>
public class FunctionResultContext
{
    private readonly FunctionResultContent _functionResult;

    public FunctionResultContext(FunctionResultContent functionResult)
    {
        _functionResult = functionResult;
    }

    /// <summary>
    /// Gets the call ID for correlating with the function call.
    /// </summary>
    public string CallId => _functionResult.CallId;

    /// <summary>
    /// Gets the raw result object.
    /// </summary>
    public object? RawResult => _functionResult.Result;

    /// <summary>
    /// Gets the result as a specific type, deserializing from JSON if necessary.
    /// </summary>
    public T? GetResult<T>()
    {
        if (_functionResult.Result is null)
        {
            return default;
        }

        if (_functionResult.Result is T typed)
        {
            return typed;
        }

        if (_functionResult.Result is JsonElement jsonElement)
        {
            return jsonElement.Deserialize<T>();
        }

        // Try to convert via JSON serialization
        var json = JsonSerializer.Serialize(_functionResult.Result);
        return JsonSerializer.Deserialize<T>(json);
    }
}
