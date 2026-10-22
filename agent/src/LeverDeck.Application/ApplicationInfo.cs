using System.Reflection;

namespace LeverDeck.Application;

public static class ApplicationInfo
{
    /// <summary>Version of the wire protocol; a client speaking another one is rejected on connect.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>The agent's informational version (for example <c>1.0.0+abc123</c>).</summary>
    public static string AgentVersion { get; } =
        typeof(ApplicationInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
