// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents binary content such as images, audio, or files in a multimodal message.
/// </summary>
/// <remarks>
/// <para>Binary content can be provided through multiple methods:</para>
/// <list type="bullet">
///   <item><description><b>Inline Data</b>: Base64-encoded content in the <see cref="Data"/> property.</description></item>
///   <item><description><b>URL Reference</b>: External URL in the <see cref="Url"/> property.</description></item>
///   <item><description><b>ID Reference</b>: Reference to pre-uploaded content via the <see cref="Id"/> property.</description></item>
/// </list>
/// <para>At least one of <see cref="Data"/>, <see cref="Url"/>, or <see cref="Id"/> must be provided.</para>
/// </remarks>
public sealed class AGUIBinaryInputContent : AGUIInputContent
{
    /// <inheritdoc />
    public override string Type => AGUIInputContentTypes.Binary;

    /// <summary>
    /// Gets or sets the MIME type of the content (e.g., "image/jpeg", "audio/wav", "application/pdf").
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional identifier for content reference (for pre-uploaded content).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets an optional URL to fetch the content from.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#pragma warning disable CA1056 // URI-like properties should not be strings - AG-UI wire format uses string
    public string? Url { get; set; }
#pragma warning restore CA1056

    /// <summary>
    /// Gets or sets the optional base64-encoded content data.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets an optional filename for the content.
    /// </summary>
    [JsonPropertyName("filename")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filename { get; set; }
}
