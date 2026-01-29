// Copyright (c) Microsoft. All rights reserved.

namespace AGUI.Protocol;

/// <summary>
/// Constants for AG-UI property names used in additional properties dictionaries.
/// </summary>
public static class AGUIPropertyNames
{
    /// <summary>
    /// The property name for the AG-UI thread identifier.
    /// </summary>
    /// <remarks>
    /// Use this constant to ensure consistent thread ID property naming across
    /// AG-UI client and server implementations.
    /// </remarks>
    public const string ThreadId = "ag_ui_thread_id";

    /// <summary>
    /// The property name for the AG-UI run identifier.
    /// </summary>
    public const string RunId = "ag_ui_run_id";
}
