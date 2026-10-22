using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Profiles;

/// <summary>A page of buttons with at most one button per position. Mutated only through <see cref="Profile"/>.</summary>
public sealed class Page
{
    private readonly List<Button> _buttons;

    public Page(int index, IEnumerable<Button> buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        Invariant.Require(index >= 0, DomainErrorCode.ValidationFailed, "Page index must not be negative.");

        _buttons = [.. buttons];
        Invariant.Require(
            _buttons.Select(button => button.Position).Distinct().Count() == _buttons.Count,
            DomainErrorCode.PositionOccupied,
            "A page holds at most one button per position.");
        Invariant.Require(
            _buttons.Select(button => button.Id).Distinct().Count() == _buttons.Count,
            DomainErrorCode.ValidationFailed,
            "Button ids must be unique.");
        Index = index;
    }

    public int Index { get; internal set; }

    public IReadOnlyList<Button> Buttons => _buttons;

    public Button? ButtonAt(GridPosition position) => _buttons.Find(button => button.Position == position);

    internal void Add(Button button) => _buttons.Add(button);

    internal void Remove(Button button) => _buttons.Remove(button);
}
