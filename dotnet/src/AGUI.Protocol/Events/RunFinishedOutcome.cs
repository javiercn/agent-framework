// Copyright (c) Microsoft. All rights reserved.

namespace AGUI.Protocol;

/// <summary>
/// Defines the outcome values for a run finished event.
/// </summary>
public static class RunFinishedOutcome
{
    /// <summary>
    /// The run completed successfully with a result.
    /// </summary>
    public const string Success = "success";

    /// <summary>
    /// The run was interrupted and is awaiting user input or approval.
    /// </summary>
    public const string Interrupt = "interrupt";
}
