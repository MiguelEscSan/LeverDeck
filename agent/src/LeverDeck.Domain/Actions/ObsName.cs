using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>The rule shared by every OBS name the agent sends: trimmed, 1–100 characters.</summary>
internal static class ObsName
{
    public const int MaxLength = 100;

    public static string Required(string? value, string what)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        Invariant.Require(
            trimmed.Length is >= 1 and <= MaxLength,
            DomainErrorCode.ValidationFailed,
            $"{what} must be 1–{MaxLength} characters.");
        return trimmed;
    }
}
