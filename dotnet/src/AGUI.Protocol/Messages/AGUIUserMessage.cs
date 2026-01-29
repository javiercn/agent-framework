// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUI.Protocol;

/// <summary>
/// Represents a user message in the AG-UI protocol.
/// </summary>
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
}
