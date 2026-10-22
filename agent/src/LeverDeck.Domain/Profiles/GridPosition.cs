using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Profiles;

/// <summary>Zero-based cell on a page. Whether it fits the grid is checked by the owning <see cref="Profile"/>.</summary>
public readonly record struct GridPosition
{
    public GridPosition(int row, int column)
    {
        Invariant.Require(row >= 0 && column >= 0, DomainErrorCode.PositionOutOfGrid, "Row and column must not be negative.");
        Row = row;
        Column = column;
    }

    public int Row { get; }

    public int Column { get; }
}
