using LeverDeck.Domain.Apps;
using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Settings;

/// <summary>
/// An application the agent may launch: trimmed display name (1–40 characters) and a fully qualified executable
/// path (at most 260 characters) with no arguments. Quotes are rejected so nothing can be appended to the command.
/// </summary>
public sealed record RegisteredApp
{
    public const int MaxDisplayNameLength = 40;
    public const int MaxPathLength = 260;

    public RegisteredApp(AppId id, string displayName, string executablePath)
    {
        Invariant.Require(id.Value != Guid.Empty, DomainErrorCode.ValidationFailed, "App id must not be empty.");

        var trimmedName = displayName?.Trim() ?? string.Empty;
        Invariant.Require(
            trimmedName.Length is >= 1 and <= MaxDisplayNameLength,
            DomainErrorCode.ValidationFailed,
            $"App display name must be 1–{MaxDisplayNameLength} characters.");

        var trimmedPath = executablePath?.Trim() ?? string.Empty;
        Invariant.Require(
            trimmedPath.Length is >= 1 and <= MaxPathLength,
            DomainErrorCode.ValidationFailed,
            $"Executable path must be 1–{MaxPathLength} characters.");
        Invariant.Require(
            !trimmedPath.Contains('"', StringComparison.Ordinal),
            DomainErrorCode.ValidationFailed,
            "Executable path must not contain quotes or arguments.");
        Invariant.Require(
            Path.IsPathFullyQualified(trimmedPath),
            DomainErrorCode.ValidationFailed,
            "Executable path must be absolute.");

        Id = id;
        DisplayName = trimmedName;
        ExecutablePath = trimmedPath;
    }

    public AppId Id { get; }

    public string DisplayName { get; }

    public string ExecutablePath { get; }
}
