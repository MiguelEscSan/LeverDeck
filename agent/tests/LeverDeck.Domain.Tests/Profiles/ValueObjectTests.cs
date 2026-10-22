using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;

namespace LeverDeck.Domain.Tests.Profiles;

public sealed class ProfileNameTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("Streaming")]
    public void Constructor_ValidName_KeepsValue(string value)
    {
        Assert.Equal(value, new ProfileName(value).Value);
    }

    [Fact]
    public void Constructor_FortyChars_IsAccepted()
    {
        var value = new string('x', 40);

        Assert.Equal(value, new ProfileName(value).Value);
    }

    [Fact]
    public void Constructor_FortyOneChars_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ProfileName(new string('x', 41)));
    }

    [Fact]
    public void Constructor_SurroundingWhitespace_IsTrimmedBeforeLengthCheck()
    {
        var name = new ProfileName($"  {new string('x', 40)}  ");

        Assert.Equal(new string('x', 40), name.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyOrNull_ThrowsValidationFailed(string? value)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ProfileName(value!));
    }

    [Fact]
    public void Equality_SameTrimmedValue_IsEqual()
    {
        Assert.Equal(new ProfileName("Games"), new ProfileName(" Games "));
    }
}

public sealed class IconIdTests
{
    [Theory]
    [InlineData("mic")]
    [InlineData("volume-up")]
    [InlineData("obs-scene-2")]
    public void Constructor_LowercaseKebabCase_IsAccepted(string value)
    {
        Assert.Equal(value, new IconId(value).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Mic")]
    [InlineData("volume_up")]
    [InlineData("-mic")]
    [InlineData("mic-")]
    [InlineData("mic--off")]
    [InlineData("mic off")]
    public void Constructor_InvalidFormat_ThrowsValidationFailed(string? value)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new IconId(value!));
    }

    [Fact]
    public void Constructor_LongerThanMaxLength_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new IconId(new string('a', IconId.MaxLength + 1)));
    }
}

public sealed class ButtonAppearanceTests
{
    private static readonly IconId _icon = new("mic");

    [Fact]
    public void Constructor_EmptyLabel_IsAccepted()
    {
        var appearance = new ButtonAppearance(string.Empty, KeyColor.Red, _icon);

        Assert.Equal(string.Empty, appearance.Label);
    }

    [Fact]
    public void Constructor_TwelveCharLabel_IsAccepted()
    {
        var appearance = new ButtonAppearance(new string('x', 12), KeyColor.Teal, _icon);

        Assert.Equal(12, appearance.Label.Length);
        Assert.Equal(KeyColor.Teal, appearance.Color);
        Assert.Equal(_icon, appearance.Icon);
    }

    [Fact]
    public void Constructor_ThirteenCharLabel_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new ButtonAppearance(new string('x', 13), KeyColor.Red, _icon));
    }

    [Fact]
    public void Constructor_NullLabel_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ButtonAppearance(null!, KeyColor.Red, _icon));
    }

    [Fact]
    public void Constructor_UndefinedColor_ThrowsValidationFailed()
    {
        DomainAssert.Violates(
            DomainErrorCode.ValidationFailed,
            () => _ = new ButtonAppearance("Mute", (KeyColor)99, _icon));
    }

    [Fact]
    public void Constructor_NullIcon_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ButtonAppearance("Mute", KeyColor.Red, null!));
    }
}

public sealed class GridSizeTests
{
    [Fact]
    public void All_ContainsExactlyTheThreeAllowedSizes()
    {
        Assert.Equal([GridSize.ThreeByThree, GridSize.FiveByThree, GridSize.EightByFour], GridSize.All);
        Assert.Equal((3, 3), (GridSize.ThreeByThree.Columns, GridSize.ThreeByThree.Rows));
        Assert.Equal((5, 3), (GridSize.FiveByThree.Columns, GridSize.FiveByThree.Rows));
        Assert.Equal((8, 4), (GridSize.EightByFour.Columns, GridSize.EightByFour.Rows));
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(5, 3)]
    [InlineData(8, 4)]
    public void From_AllowedSize_ReturnsStaticInstance(int columns, int rows)
    {
        var size = GridSize.From(columns, rows);

        Assert.Contains(size, GridSize.All);
        Assert.Equal((columns, rows), (size.Columns, size.Rows));
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(3, 5)]
    [InlineData(0, 0)]
    public void From_DisallowedSize_ThrowsValidationFailed(int columns, int rows)
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => GridSize.From(columns, rows));
    }

    [Fact]
    public void Contains_LastCellInside_FirstCellOutside()
    {
        Assert.True(GridSize.FiveByThree.Contains(new GridPosition(2, 4)));
        Assert.False(GridSize.FiveByThree.Contains(new GridPosition(3, 0)));
        Assert.False(GridSize.FiveByThree.Contains(new GridPosition(0, 5)));
    }
}

public sealed class GridPositionTests
{
    [Fact]
    public void Constructor_ZeroZero_IsAccepted()
    {
        var position = new GridPosition(0, 0);

        Assert.Equal((0, 0), (position.Row, position.Column));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Constructor_Negative_ThrowsPositionOutOfGrid(int row, int column)
    {
        DomainAssert.Violates(DomainErrorCode.PositionOutOfGrid, () => _ = new GridPosition(row, column));
    }
}

public sealed class IdTests
{
    [Fact]
    public void ProfileId_New_IsUniqueAndNotEmpty()
    {
        var first = ProfileId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, ProfileId.New());
    }

    [Fact]
    public void ButtonId_New_IsUniqueAndNotEmpty()
    {
        var first = ButtonId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, ButtonId.New());
    }

    [Fact]
    public void Ids_EmptyGuid_ThrowsValidationFailed()
    {
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ProfileId(Guid.Empty));
        DomainAssert.Violates(DomainErrorCode.ValidationFailed, () => _ = new ButtonId(Guid.Empty));
    }

    [Fact]
    public void Ids_SameGuid_AreEqual()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(new ProfileId(guid), new ProfileId(guid));
        Assert.Equal(new ButtonId(guid), new ButtonId(guid));
    }
}
