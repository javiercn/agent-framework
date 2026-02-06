// Copyright (c) Microsoft. All rights reserved.

namespace AGUI.Protocol;

/// <summary>
/// Represents a developer message in the AG-UI protocol.
/// </summary>
public sealed class AGUIDeveloperMessage : AGUIMessage
{
    /// <inheritdoc />
    public override string Role => AGUIRoles.Developer;
}
