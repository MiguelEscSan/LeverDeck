namespace LeverDeck.Application.Security;

/// <summary>Who is calling a use case, from least to most privileged.</summary>
public enum CallerScope
{
    /// <summary>Not authenticated: may only connect a device or pair one.</summary>
    Public,

    /// <summary>A paired device on its authenticated connection.</summary>
    Device,

    /// <summary>The desktop editor on the PC. How it authenticates is decided by the transport (Phase 3).</summary>
    Admin,
}
