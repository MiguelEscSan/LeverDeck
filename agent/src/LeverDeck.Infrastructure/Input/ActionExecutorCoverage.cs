using LeverDeck.Domain.Actions;

namespace LeverDeck.Infrastructure.Input;

/// <summary>How the agent's executor treats an action type.</summary>
public enum ActionSupport
{
    /// <summary>Executed now.</summary>
    Executable,

    /// <summary>An agent action whose adapter arrives later (OBS); it fails with <c>actionNotExecutable</c>.</summary>
    NotExecutableYet,

    /// <summary>Runs on the client and never reaches the executor.</summary>
    ClientOnly,
}

/// <summary>
/// The executor's routing, as data. Architecture tests check it lists exactly <see cref="ActionCatalog.All"/>, and the
/// executor tests check <see cref="AgentActionExecutor"/> behaves as it says.
/// </summary>
public static class ActionExecutorCoverage
{
    public static IReadOnlyDictionary<Type, ActionSupport> ByActionType { get; } = new Dictionary<Type, ActionSupport>
    {
        [typeof(Hotkey)] = ActionSupport.Executable,
        [typeof(MediaKey)] = ActionSupport.Executable,
        [typeof(LaunchApp)] = ActionSupport.Executable,
        [typeof(OpenUrl)] = ActionSupport.Executable,
        [typeof(ObsScene)] = ActionSupport.NotExecutableYet,
        [typeof(LaunchGame)] = ActionSupport.Executable,
        [typeof(ChangePage)] = ActionSupport.ClientOnly,
        [typeof(SwitchProfile)] = ActionSupport.ClientOnly,
    };
}
