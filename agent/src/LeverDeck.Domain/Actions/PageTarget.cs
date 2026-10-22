using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

public enum PageTargetKind
{
    Next,
    Previous,
    Specific,
}

/// <summary>Destination of a <see cref="ChangePage"/>: the next page, the previous page or a specific page index.</summary>
public sealed record PageTarget
{
    private PageTarget(PageTargetKind kind, int? index)
    {
        Kind = kind;
        Index = index;
    }

    public static PageTarget Next { get; } = new(PageTargetKind.Next, null);

    public static PageTarget Previous { get; } = new(PageTargetKind.Previous, null);

    public PageTargetKind Kind { get; }

    /// <summary>The zero-based page index when <see cref="Kind"/> is <see cref="PageTargetKind.Specific"/>.</summary>
    public int? Index { get; }

    /// <summary>A specific page. Whether it exists is resolved by the client against the active profile.</summary>
    public static PageTarget At(int index)
    {
        Invariant.Require(index >= 0, DomainErrorCode.ValidationFailed, "Page index must not be negative.");
        return new PageTarget(PageTargetKind.Specific, index);
    }
}
