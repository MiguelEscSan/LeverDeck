using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Domain.Tests.Profiles;

public sealed class PageTests
{
    private static readonly ButtonAppearance _appearance = new("Mute", KeyColor.Red, new IconId("mic"));
    private static readonly DomainAction _action = new MediaKey(MediaKeyKind.Mute);

    [Fact]
    public void Constructor_DistinctPositions_KeepsButtons()
    {
        var first = new Button(ButtonId.New(), new GridPosition(0, 0), _appearance, _action);
        var second = new Button(ButtonId.New(), new GridPosition(0, 1), _appearance, _action);

        var page = new Page(2, [first, second]);

        Assert.Equal(2, page.Index);
        Assert.Equal([first, second], page.Buttons);
        Assert.Same(second, page.ButtonAt(new GridPosition(0, 1)));
        Assert.Null(page.ButtonAt(new GridPosition(1, 1)));
    }

    [Fact]
    public void Constructor_TwoButtonsOnSamePosition_ThrowsPositionOccupied()
    {
        var position = new GridPosition(1, 1);

        DomainAssert.Violates(
            DomainErrorCode.PositionOccupied,
            () => _ = new Page(
                0,
                [new Button(ButtonId.New(), position, _appearance, _action), new Button(ButtonId.New(), position, _appearance, _action)]));
    }

    [Fact]
    public void Constructor_DuplicateButtonId_ThrowsValidationFailed()
    {
        var id = ButtonId.New();

        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new Page(
                0,
                [new Button(id, new GridPosition(0, 0), _appearance, _action), new Button(id, new GridPosition(0, 1), _appearance, _action)]));
    }

    [Fact]
    public void Constructor_NegativeIndex_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new Page(-1, []));
    }

    [Fact]
    public void Button_NullAppearance_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Button(ButtonId.New(), new GridPosition(0, 0), null!, _action));
    }

    [Fact]
    public void Button_NullAction_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Button(ButtonId.New(), new GridPosition(0, 0), _appearance, null!));
    }

    [Fact]
    public void Button_Constructor_KeepsAction()
    {
        var button = new Button(ButtonId.New(), new GridPosition(0, 0), _appearance, _action);

        Assert.Equal(_action, button.Action);
    }
}
