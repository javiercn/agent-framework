// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents a user message in the AG-UI protocol.
/// </summary>
/// <remarks>
/// <para>Content is always represented as an array of <see cref="AGUIInputContent"/> items.</para>
/// <para>For simple text messages, use a single <see cref="AGUITextInputContent"/> element.</para>
/// <para>The JSON converter automatically handles the wire format union type (<c>string | InputContent[]</c>):</para>
/// <list type="bullet">
///   <item><description><b>Deserialization</b>: String content is wrapped in <see cref="AGUITextInputContent"/>.</description></item>
///   <item><description><b>Serialization</b>: Single text content is emitted as a string for wire compatibility.</description></item>
/// </list>
/// </remarks>
public sealed class AGUIUserMessage : AGUIMessage
{
    /// <inheritdoc />
    public override string Role => AGUIRoles.User;

    /// <summary>
    /// Gets or sets the name of the user.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the message content as an array of input content items.
    /// </summary>
    /// <remarks>
    /// This property hides the base <see cref="AGUIMessage.Content"/> string property.
    /// The JSON converter handles the wire format union type (<c>string | InputContent[]</c>).
    /// </remarks>
    [JsonIgnore] // Handled by custom converter in AGUIMessageJsonConverter
    public new IList<AGUIInputContent> Content { get; set; } = [];
}
