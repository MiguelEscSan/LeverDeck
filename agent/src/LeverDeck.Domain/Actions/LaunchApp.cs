using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>
/// Launches an application registered in the agent settings. It carries only the id: the client can never
/// supply a path or arguments.
/// </summary>
public sealed record LaunchApp : Action
{
    public LaunchApp(AppId app)
    {
        Invariant.Require(app.Value != Guid.Empty, DomainErrorCode.ValidationFailed, "App id must not be empty.");
        App = app;
    }

    public AppId App { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
