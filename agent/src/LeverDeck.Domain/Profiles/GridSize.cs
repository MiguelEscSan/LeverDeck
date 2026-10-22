using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Profiles;

/// <summary>Columns × rows of a profile's pages. Only 3×3, 5×3 and 8×4 exist.</summary>
public sealed record GridSize
{
    private GridSize(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public static GridSize ThreeByThree { get; } = new(3, 3);

    public static GridSize FiveByThree { get; } = new(5, 3);

    public static GridSize EightByFour { get; } = new(8, 4);

    public static IReadOnlyList<GridSize> All { get; } = [ThreeByThree, FiveByThree, EightByFour];

    public int Columns { get; }

    public int Rows { get; }

    public static GridSize From(int columns, int rows) =>
        All.FirstOrDefault(size => size.Columns == columns && size.Rows == rows)
        ?? throw new InvariantViolationException(
            DomainErrorCode.ValidationFailed,
            $"Grid size {columns}×{rows} is not allowed; use 3×3, 5×3 or 8×4.");

    public bool Contains(GridPosition position) => position.Row < Rows && position.Column < Columns;
}
