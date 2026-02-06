// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Base class for multimodal input content in AG-UI messages.
/// </summary>
[JsonConverter(typeof(AGUIInputContentJsonConverter))]
public abstract class AGUIInputContent
{
    /// <summary>
    /// Gets the content type discriminator ("text" or "binary").
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}
