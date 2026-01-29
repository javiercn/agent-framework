// Copyright (c) Microsoft. All rights reserved.

namespace AGUI.Protocol;

/// <summary>
/// Constants for AG-UI message roles.
/// </summary>
public static class AGUIRoles
{
    /// <summary>
    /// The system role for system messages.
    /// </summary>
    public const string System = "system";

    /// <summary>
    /// The user role for user messages.
    /// </summary>
    public const string User = "user";

    /// <summary>
    /// The assistant role for assistant messages.
    /// </summary>
    public const string Assistant = "assistant";

    /// <summary>
    /// The developer role for developer messages.
    /// </summary>
    public const string Developer = "developer";

    /// <summary>
    /// The tool role for tool result messages.
    /// </summary>
    public const string Tool = "tool";
}
