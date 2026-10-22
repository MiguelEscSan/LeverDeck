using LeverDeck.Application.Profiles;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Profiles;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Infrastructure.Tests.ProtocolMapping;

public sealed class ProfileMapperTests
{
    private static readonly Guid _profileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _buttonId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _otherButtonId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static ButtonDto Button(Guid? id = null, int row = 0, int column = 0, string label = "Copy", string iconId = "content-copy") =>
        new(id ?? _buttonId, row, column, label, KeyColorDto.Teal, iconId, new HotkeyActionDto([KeyModifierDto.Ctrl], VirtualKeyDto.C));

    private static ProfileDto Profile(params PageDto[] pages) =>
        new(_profileId, "Streaming", KeyColorDto.Purple, new GridDto(5, 3), pages.Length == 0 ? [new PageDto(0, [Button()])] : pages);

    [Fact]
    public void ToDomain_MapsEveryField()
    {
        var profile = MappingAssert.Succeeds(ProfileMapper.ToDomain(Profile()));

        Assert.Equal(new ProfileId(_profileId), profile.Id);
        Assert.Equal("Streaming", profile.Name.Value);
        Assert.Equal(KeyColor.Purple, profile.Color);
        Assert.Equal(GridSize.FiveByThree, profile.GridSize);
        var button = Assert.Single(Assert.Single(profile.Pages).Buttons);
        Assert.Equal(new ButtonId(_buttonId), button.Id);
        Assert.Equal(new GridPosition(0, 0), button.Position);
        Assert.Equal(new ButtonAppearance("Copy", KeyColor.Teal, new IconId("content-copy")), button.Appearance);
        Assert.Equal(new Hotkey(KeyModifiers.Ctrl, VirtualKey.C), button.Action);
    }

    [Fact]
    public void Profile_SurvivesDtoToDomainToDto()
    {
        var dto = Profile(
            new PageDto(0, [Button(), Button(_otherButtonId, row: 2, column: 4, label: string.Empty, iconId: "key")]),
            new PageDto(1, []));

        var back = ProfileMapper.ToDto(MappingAssert.Succeeds(ProfileMapper.ToDomain(dto)));

        MappingAssert.SameJson(dto, back);
    }

    [Fact]
    public void ToSummaryDto_KeepsIdNameAndColor()
    {
        var summary = new ProfileSummary(new ProfileId(_profileId), new ProfileName("Work"), KeyColor.Orange);

        Assert.Equal(new ProfileSummaryDto(_profileId, "Work", KeyColorDto.Orange), ProfileMapper.ToSummaryDto(summary));
    }

    [Fact]
    public void Grid_MapsBothWays()
    {
        Assert.Equal(new GridDto(8, 4), GridMapper.ToDto(GridSize.EightByFour));
        Assert.Equal(GridSize.ThreeByThree, MappingAssert.Succeeds(GridMapper.ToDomain(new GridDto(3, 3))));
        MappingAssert.IsValidationFailure(GridMapper.ToDomain(new GridDto(4, 4)));
        MappingAssert.IsValidationFailure(GridMapper.ToDomain(null!));
    }

    public static TheoryData<int> InvalidProfileIndexes() => [.. Enumerable.Range(0, _invalidProfiles.Length)];

    private static readonly ProfileDto[] _invalidProfiles =
    [
        Profile() with { Id = Guid.Empty },
        Profile() with { Name = "  " },
        Profile() with { Name = null! },
        Profile() with { Color = (KeyColorDto)40 },
        Profile() with { Grid = new GridDto(4, 4) },
        Profile() with { Grid = null! },
        Profile() with { Pages = null! },
        Profile() with { Pages = [] },
        Profile() with { Pages = [null!] },
        Profile(new PageDto(1, [])),
        Profile(new PageDto(0, null!)),
        Profile(new PageDto(0, [null!])),
        Profile(new PageDto(0, [Button(row: 3)])),
        Profile(new PageDto(0, [Button(row: -1)])),
        Profile(new PageDto(0, [Button(), Button(_otherButtonId)])),
        Profile(new PageDto(0, [Button()]), new PageDto(1, [Button(column: 1)])),
        Profile(new PageDto(0, [Button(id: Guid.Empty)])),
        Profile(new PageDto(0, [Button(label: "Thirteen chars")])),
        Profile(new PageDto(0, [Button(iconId: "Not Kebab")])),
        Profile(new PageDto(0, [Button() with { Color = (KeyColorDto)9 }])),
        Profile(new PageDto(0, [Button() with { Action = null! }])),
        Profile(new PageDto(0, [Button() with { Action = new OpenUrlActionDto("javascript:alert(1)") }])),
    ];

    [Theory]
    [MemberData(nameof(InvalidProfileIndexes))]
    public void InvalidProfile_BecomesValidationFailure(int index)
    {
        MappingAssert.IsValidationFailure(ProfileMapper.ToDomain(_invalidProfiles[index]));
    }

    [Fact]
    public void NullProfile_BecomesValidationFailure()
    {
        MappingAssert.IsValidationFailure(ProfileMapper.ToDomain(null!));
    }
}
