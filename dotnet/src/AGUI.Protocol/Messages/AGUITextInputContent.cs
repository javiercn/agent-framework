// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents text content within a multimodal message.
/// </summary>
public sealed class AGUITextInputContent : AGUIInputContent
{
    /// <inheritdoc />
    public override string Type => AGUIInputContentTypes.Text;

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
