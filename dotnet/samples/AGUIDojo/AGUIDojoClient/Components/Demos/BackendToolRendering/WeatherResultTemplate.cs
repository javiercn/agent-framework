// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUIDojoClient.Components.Shared;
using Microsoft.Extensions.AI;

namespace Microsoft.AspNetCore.Components.AI;

/// <summary>
/// Template for rendering weather function result content.
/// </summary>
public class WeatherResultTemplate : ContentTemplateBase
{
    [CascadingParameter] internal MessageListContext Context { get; set; } = default!;

    public override void Attach(RenderHandle renderHandle)
    {
        // This component never renders anything by itself.
        this.ChildContent = this.RenderWeatherResult;
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.Context.RegisterContentTemplate(this);
        return Task.CompletedTask;
    }

    public new bool When(ContentContext context)
    {
        // Only match FunctionResultContent
        return context.Content is FunctionResultContent;
    }

    private RenderFragment RenderWeatherResult(ContentContext content) => builder =>
    {
        if (content.Content is FunctionResultContent functionResult)
        {
            var weather = DeserializeResult<WeatherInfo>(functionResult.Result);
            if (weather != null)
            {
                builder.OpenComponent<AGUIDojoClient.Components.Demos.BackendToolRendering.WeatherCard>(0);
                builder.AddAttribute(1, "Weather", weather);
                builder.AddAttribute(2, "Location", "Requested Location");
                builder.CloseComponent();
            }
            else
            {
                // Fallback: render raw result as text
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "function-result");
                builder.AddContent(2, functionResult.Result?.ToString() ?? "No result");
                builder.CloseElement();
            }
        }
    };

    private static T? DeserializeResult<T>(object? result)
    {
        if (result is null)
        {
            return default;
        }

        if (result is T typed)
        {
            return typed;
        }

        if (result is JsonElement jsonElement)
        {
            return jsonElement.Deserialize<T>();
        }

        // Try to convert via JSON serialization
        var json = JsonSerializer.Serialize(result);
        return JsonSerializer.Deserialize<T>(json);
    }
}
