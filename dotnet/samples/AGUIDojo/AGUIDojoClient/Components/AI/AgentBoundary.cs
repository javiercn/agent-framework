// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.AI;

[CascadingTypeParameter(nameof(TState))]
public class AgentStateBoundary<TState> : IComponent, IDisposable
{
    private RenderHandle _renderHandle;
    private RenderFragment? _renderWithState;
    private protected AgentBoundaryContext<TState>? _context;
    private AgentThread? _currentThread;
    private AIAgent? _currentAgent;
    private bool _disposed;

    [EditorRequired][Parameter] public AIAgent? Agent { get; set; }

    [Parameter] public AgentThread? Thread { get; set; }

    [Parameter] public TState? State { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    public void Attach(RenderHandle renderHandle)
    {
        this._renderHandle = renderHandle;
        this._renderWithState = this.RenderWithState;
    }

    // Agent boundary renders once when it receives the initial set of parameters
    // and only ever renders again if the Agent, Thread, or State changes.
    // Re-rendering the agent boundary will dispose the existing context and create
    // a new one.
    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);

        if (this.Agent == null)
        {
            throw new InvalidOperationException("AgentBoundary requires an Agent parameter.");
        }

        var thread = this.Thread ?? this.Agent.GetNewThread();

        TState? currentState = this._context != null ? this._context.CurrentState : default;

        bool refresh = this.Agent != this._currentAgent ||
            thread != this._currentThread ||
            !EqualityComparer<TState?>.Default.Equals(this.State, currentState);

        if (refresh)
        {
            this._context?.Dispose();
            this._context = null;
            this._currentThread = null;
        }

        this._currentAgent = this.Agent;
        this._currentThread = thread;

        this._context ??= new AgentBoundaryContext<TState>(this._currentAgent, this._currentThread);
        this._context.CurrentState = this.State;

        if (refresh)
        {
            this.Render();
        }

        return Task.CompletedTask;
    }

    private void Render()
    {
        this._renderHandle.Render(builder =>
        {
            builder.OpenComponent<CascadingValue<AgentBoundaryContext<TState>>>(0);
            builder.AddComponentParameter(1, "Value", this._context);
            builder.AddComponentParameter(2, "IsFixed", true);
            builder.AddComponentParameter(3, "ChildContent", this._renderWithState);
            builder.CloseComponent();
        });
    }

    protected virtual void RenderWithState(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<TState?>>(0);
        builder.AddComponentParameter(1, "Value", this.State);
        builder.AddComponentParameter(2, "IsFixed", false);
        builder.AddComponentParameter(3, "ChildContent", this.ChildContent);
        builder.CloseComponent();
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposed)
        {
            if (disposing)
            {
                this._context?.Dispose();
            }
            this._disposed = true;
        }
    }
}

public class AgentBoundary : AgentStateBoundary<object?>
{
    protected override void RenderWithState(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<IAgentBoundaryContext>>(0);
        builder.AddComponentParameter(1, "Value", this._context);
        builder.AddComponentParameter(2, "IsFixed", true);
        builder.AddComponentParameter(3, "ChildContent", this.ChildContent);
        builder.CloseComponent();
    }
}
