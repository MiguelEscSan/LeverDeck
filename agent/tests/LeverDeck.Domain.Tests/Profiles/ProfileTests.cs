using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Domain.Tests.Profiles;

public sealed class ProfileTests
{
    private static readonly ButtonAppearance _mute = new("Mute", KeyColor.Red, new IconId("mic"));
    private static readonly ButtonAppearance _scene = new("Scene", KeyColor.Purple, new IconId("obs"));
    private static readonly DomainAction _mic = new MediaKey(MediaKeyKind.Mute);
    private static readonly DomainAction _obs = new ObsScene("Gameplay");

    private static Profile NewProfile(GridSize? gridSize = null) =>
        Profile.Create(new ProfileName("Streaming"), KeyColor.Blue, gridSize ?? GridSize.FiveByThree);

    private static Profile ProfileWithPages(int pageCount)
    {
        var profile = NewProfile();
        while (profile.Pages.Count < pageCount)
        {
            profile.AddPage();
        }

        return profile;
    }

    // Create / Restore

    [Fact]
    public void Create_StartsWithOneEmptyPage()
    {
        var profile = NewProfile();

        Assert.NotEqual(Guid.Empty, profile.Id.Value);
        Assert.Equal(new ProfileName("Streaming"), profile.Name);
        Assert.Equal(KeyColor.Blue, profile.Color);
        Assert.Equal(GridSize.FiveByThree, profile.GridSize);
        var page = Assert.Single(profile.Pages);
        Assert.Equal(0, page.Index);
        Assert.Empty(page.Buttons);
    }

    [Fact]
    public void Create_UndefinedColor_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => Profile.Create(new ProfileName("x"), (KeyColor)42, GridSize.ThreeByThree));
    }

    [Fact]
    public void Restore_ValidPages_KeepsState()
    {
        var id = ProfileId.New();
        var button = new Button(ButtonId.New(), new GridPosition(3, 7), _mute, _mic);

        var profile = Restore(id, GridSize.EightByFour, new Page(0, []), new Page(1, [button]));

        Assert.Equal(id, profile.Id);
        Assert.Equal(2, profile.Pages.Count);
        Assert.Same(button, profile.FindButton(button.Id));
    }

    [Fact]
    public void Restore_NoPages_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => Restore(ProfileId.New(), GridSize.ThreeByThree));
    }

    [Fact]
    public void Restore_TenPages_IsAccepted()
    {
        var pages = Enumerable.Range(0, 10).Select(index => new Page(index, [])).ToArray();

        Assert.Equal(10, Restore(ProfileId.New(), GridSize.ThreeByThree, pages).Pages.Count);
    }

    [Fact]
    public void Restore_ElevenPages_ThrowsValidationFailed()
    {
        var pages = Enumerable.Range(0, 11).Select(index => new Page(index, [])).ToArray();

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => Restore(ProfileId.New(), GridSize.ThreeByThree, pages));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void Restore_NonContiguousOrUnorderedIndexes_ThrowsValidationFailed(int first, int second)
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => Restore(ProfileId.New(), GridSize.ThreeByThree, new Page(first, []), new Page(second, [])));
    }

    [Fact]
    public void Restore_ButtonOutsideGrid_ThrowsPositionOutOfGrid()
    {
        var button = new Button(ButtonId.New(), new GridPosition(3, 0), _mute, _mic);

        DomainAssert.Violates(
            DomainErrorCode.PositionOutOfGrid,
            () => Restore(ProfileId.New(), GridSize.ThreeByThree, new Page(0, [button])));
    }

    [Fact]
    public void Restore_SameButtonIdOnTwoPages_ThrowsValidationFailed()
    {
        var id = ButtonId.New();

        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => Restore(
                ProfileId.New(),
                GridSize.ThreeByThree,
                new Page(0, [new Button(id, new GridPosition(0, 0), _mute, _mic)]),
                new Page(1, [new Button(id, new GridPosition(0, 0), _mute, _mic)])));
    }

    // Rename / ChangeColor

    [Fact]
    public void Rename_ReplacesName()
    {
        var profile = NewProfile();

        profile.Rename(new ProfileName("Gaming"));

        Assert.Equal(new ProfileName("Gaming"), profile.Name);
    }

    [Fact]
    public void ChangeColor_UndefinedColor_ThrowsValidationFailed()
    {
        var profile = NewProfile();

        profile.ChangeColor(KeyColor.Orange);

        Assert.Equal(KeyColor.Orange, profile.Color);
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => profile.ChangeColor((KeyColor)7));
    }

    // Pages

    [Fact]
    public void AddPage_AppendsPageWithNextIndex()
    {
        var profile = NewProfile();

        var page = profile.AddPage();

        Assert.Equal(1, page.Index);
        Assert.Equal([0, 1], profile.Pages.Select(p => p.Index));
    }

    [Fact]
    public void AddPage_UpToTenPages_IsAccepted()
    {
        Assert.Equal(Profile.MaxPages, ProfileWithPages(10).Pages.Count);
    }

    [Fact]
    public void AddPage_EleventhPage_ThrowsValidationFailed()
    {
        var profile = ProfileWithPages(10);

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => profile.AddPage());
        Assert.Equal(10, profile.Pages.Count);
    }

    [Fact]
    public void RemovePage_MiddlePage_ReindexesRemainingPages()
    {
        var profile = ProfileWithPages(3);
        var buttonId = profile.PlaceButton(2, new GridPosition(0, 0), _mute, _mic);

        profile.RemovePage(1);

        Assert.Equal([0, 1], profile.Pages.Select(p => p.Index));
        Assert.Same(profile.Pages[1], profile.Pages.Single(p => p.Buttons.Any(b => b.Id == buttonId)));
    }

    [Fact]
    public void RemovePage_LastRemainingPage_ThrowsValidationFailed()
    {
        var profile = NewProfile();

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => profile.RemovePage(0));
        Assert.Single(profile.Pages);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void RemovePage_UnknownIndex_ThrowsValidationFailed(int pageIndex)
    {
        var profile = ProfileWithPages(2);

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => profile.RemovePage(pageIndex));
    }

    // Buttons

    [Fact]
    public void PlaceButton_FreePosition_AddsButton()
    {
        var profile = NewProfile();
        var position = new GridPosition(2, 4);

        var id = profile.PlaceButton(0, position, _mute, _mic);

        var button = Assert.Single(profile.Pages[0].Buttons);
        Assert.Equal(id, button.Id);
        Assert.Equal(position, button.Position);
        Assert.Equal(_mute, button.Appearance);
        Assert.Equal(_mic, button.Action);
    }

    [Fact]
    public void PlaceButton_NullAction_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => NewProfile().PlaceButton(0, new GridPosition(0, 0), _mute, null!));
    }

    [Fact]
    public void PlaceButton_OccupiedPosition_ThrowsPositionOccupied()
    {
        var profile = NewProfile();
        profile.PlaceButton(0, new GridPosition(1, 1), _mute, _mic);

        DomainAssert.Violates(DomainErrorCode.PositionOccupied, () => profile.PlaceButton(0, new GridPosition(1, 1), _scene, _mic));
    }

    [Fact]
    public void PlaceButton_SamePositionOnAnotherPage_IsAccepted()
    {
        var profile = ProfileWithPages(2);
        profile.PlaceButton(0, new GridPosition(1, 1), _mute, _mic);

        profile.PlaceButton(1, new GridPosition(1, 1), _scene, _mic);

        Assert.Single(profile.Pages[1].Buttons);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(0, 5)]
    public void PlaceButton_OutsideGrid_ThrowsPositionOutOfGrid(int row, int column)
    {
        var profile = NewProfile(GridSize.FiveByThree);

        DomainAssert.Violates(DomainErrorCode.PositionOutOfGrid, () => profile.PlaceButton(0, new GridPosition(row, column), _mute, _mic));
    }

    [Fact]
    public void PlaceButton_UnknownPage_ThrowsValidationFailed()
    {
        var profile = NewProfile();

        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => profile.PlaceButton(1, new GridPosition(0, 0), _mute, _mic));
    }

    [Fact]
    public void ChangeButtonAppearance_ExistingButton_ReplacesAppearance()
    {
        var profile = NewProfile();
        var id = profile.PlaceButton(0, new GridPosition(0, 0), _mute, _mic);

        profile.ChangeButtonAppearance(id, _scene);

        Assert.Equal(_scene, profile.FindButton(id)!.Appearance);
    }

    [Fact]
    public void ChangeButtonAppearance_UnknownButton_ThrowsButtonNotFound()
    {
        DomainAssert.Violates(DomainErrorCode.ButtonNotFound, () => NewProfile().ChangeButtonAppearance(ButtonId.New(), _scene));
    }

    [Fact]
    public void ChangeButtonAction_ExistingButton_ReplacesActionAndKeepsAppearance()
    {
        var profile = NewProfile();
        var id = profile.PlaceButton(0, new GridPosition(0, 0), _mute, _mic);

        profile.ChangeButtonAction(id, _obs);

        var button = profile.FindButton(id)!;
        Assert.Equal(_obs, button.Action);
        Assert.Equal(_mute, button.Appearance);
    }

    [Fact]
    public void ChangeButtonAction_SwitchToOwnProfile_IsAccepted()
    {
        var profile = NewProfile();
        var id = profile.PlaceButton(0, new GridPosition(0, 0), _mute, _mic);

        profile.ChangeButtonAction(id, new SwitchProfile(profile.Id));

        Assert.Equal(new SwitchProfile(profile.Id), profile.FindButton(id)!.Action);
    }

    [Fact]
    public void ChangeButtonAction_UnknownButton_ThrowsButtonNotFound()
    {
        DomainAssert.Violates(DomainErrorCode.ButtonNotFound, () => NewProfile().ChangeButtonAction(ButtonId.New(), _obs));
    }

    [Fact]
    public void ChangeButtonAction_NullAction_ThrowsArgumentNull()
    {
        var profile = NewProfile();
        var id = profile.PlaceButton(0, new GridPosition(0, 0), _mute, _mic);

        Assert.Throws<ArgumentNullException>(() => profile.ChangeButtonAction(id, null!));
        Assert.Equal(_mic, profile.FindButton(id)!.Action);
    }

    [Fact]
    public void RemoveButton_ExistingButton_FreesPosition()
    {
        var profile = NewProfile();
        var id = profile.PlaceButton(0, new GridPosition(0, 0), _mute, _mic);

        profile.RemoveButton(id);

        Assert.Empty(profile.Pages[0].Buttons);
        Assert.Null(profile.FindButton(id));
        profile.PlaceButton(0, new GridPosition(0, 0), _scene, _mic);
    }

    [Fact]
    public void RemoveButton_UnknownButton_ThrowsButtonNotFound()
    {
        DomainAssert.Violates(DomainErrorCode.ButtonNotFound, () => NewProfile().RemoveButton(ButtonId.New()));
    }

    [Fact]
    public void MoveButton_FreePosition_MovesButton()
    {
        var profile = NewProfile();
        var id = profile.PlaceButton(0, new GridPosition(0, 0), _mute, _mic);

        profile.MoveButton(id, new GridPosition(2, 4));

        Assert.Equal(new GridPosition(2, 4), profile.FindButton(id)!.Position);
        Assert.Null(profile.Pages[0].ButtonAt(new GridPosition(0, 0)));
    }

    [Fact]
    public void MoveButton_SamePosition_IsNoOp()
    {
        var profile = NewProfile();
        var id = profile.PlaceButton(0, new GridPosition(1, 1), _mute, _mic);

        profile.MoveButton(id, new GridPosition(1, 1));

        Assert.Equal(new GridPosition(1, 1), profile.FindButton(id)!.Position);
    }

    [Fact]
    public void MoveButton_OccupiedPosition_ThrowsPositionOccupiedAndKeepsBoth()
    {
        var profile = NewProfile();
        var moving = profile.PlaceButton(0, new GridPosition(0, 0), _mute, _mic);
        var other = profile.PlaceButton(0, new GridPosition(0, 1), _scene, _mic);

        DomainAssert.Violates(DomainErrorCode.PositionOccupied, () => profile.MoveButton(moving, new GridPosition(0, 1)));
        Assert.Equal(new GridPosition(0, 0), profile.FindButton(moving)!.Position);
        Assert.Equal(new GridPosition(0, 1), profile.FindButton(other)!.Position);
    }

    [Fact]
    public void MoveButton_OutsideGrid_ThrowsPositionOutOfGrid()
    {
        var profile = NewProfile(GridSize.ThreeByThree);
        var id = profile.PlaceButton(0, new GridPosition(0, 0), _mute, _mic);

        DomainAssert.Violates(DomainErrorCode.PositionOutOfGrid, () => profile.MoveButton(id, new GridPosition(0, 3)));
    }

    [Fact]
    public void MoveButton_UnknownButton_ThrowsButtonNotFound()
    {
        DomainAssert.Violates(DomainErrorCode.ButtonNotFound, () => NewProfile().MoveButton(ButtonId.New(), new GridPosition(0, 0)));
    }

    // Grid size

    [Fact]
    public void ChangeGridSize_Grow_IsAccepted()
    {
        var profile = NewProfile(GridSize.ThreeByThree);
        profile.PlaceButton(0, new GridPosition(2, 2), _mute, _mic);

        profile.ChangeGridSize(GridSize.EightByFour);

        Assert.Equal(GridSize.EightByFour, profile.GridSize);
    }

    [Fact]
    public void ChangeGridSize_ShrinkWithAllButtonsInside_IsAccepted()
    {
        var profile = NewProfile(GridSize.EightByFour);
        profile.PlaceButton(0, new GridPosition(2, 2), _mute, _mic);

        profile.ChangeGridSize(GridSize.ThreeByThree);

        Assert.Equal(GridSize.ThreeByThree, profile.GridSize);
    }

    [Fact]
    public void ChangeGridSize_ButtonWouldFallOutside_ThrowsGridTooSmallAndKeepsSize()
    {
        var profile = ProfileWithPages(2);
        profile.PlaceButton(1, new GridPosition(0, 4), _mute, _mic);

        DomainAssert.Violates(DomainErrorCode.GridTooSmall, () => profile.ChangeGridSize(GridSize.ThreeByThree));
        Assert.Equal(GridSize.FiveByThree, profile.GridSize);
    }

    private static Profile Restore(ProfileId id, GridSize gridSize, params Page[] pages) =>
        Profile.Restore(id, new ProfileName("Restored"), KeyColor.Dark, gridSize, pages);
}
