// Copyright (c) Microsoft. All rights reserved.

namespace AGUI.Protocol;

/// <summary>
/// Represents a system message in the AG-UI protocol.
/// </summary>
public sealed class AGUISystemMessage : AGUIMessage
{
    /// <inheritdoc />
    public override string Role => AGUIRoles.System;
}
