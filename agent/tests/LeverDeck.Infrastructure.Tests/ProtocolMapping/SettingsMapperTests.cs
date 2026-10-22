using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Settings;
using LeverDeck.Infrastructure.ProtocolMapping;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Infrastructure.Tests.ProtocolMapping;

public sealed class SettingsMapperTests
{
    private static readonly ObsConnection _storedObs = new("localhost", 4455, "stored-secret");

    [Fact]
    public void ToDto_ExposesOnlyWhetherAPasswordIsSet()
    {
        var settings = AgentSettings.CreateDefault();
        settings.ConfigureObs(_storedObs);
        var appId = settings.RegisterApp("Notepad", @"C:\Windows\notepad.exe");

        var dto = SettingsMapper.ToDto(settings);

        Assert.False(dto.StartWithWindows);
        Assert.Equal(new GridDto(5, 3), dto.DefaultGrid);
        Assert.Equal(new ObsSettingsDto("localhost", 4455, true), dto.Obs);
        Assert.Equal(new[] { new RegisteredAppDto(appId.Value, "Notepad", @"C:\Windows\notepad.exe") }, dto.Apps);
        Assert.DoesNotContain("stored-secret", MappingAssert.Json(dto), StringComparison.Ordinal);
    }

    [Fact]
    public void ToDto_WithoutObs_HasNullObs()
    {
        Assert.Null(SettingsMapper.ToDto(AgentSettings.CreateDefault()).Obs);
    }

    [Fact]
    public void ToUpdate_MapsGeneralSettings()
    {
        var update = MappingAssert.Succeeds(SettingsMapper.ToUpdate(new SettingsUpdateDto(true, new GridDto(8, 4), null), _storedObs));

        Assert.True(update.StartWithWindows);
        Assert.Equal(GridSize.EightByFour, update.DefaultGridSize);
        Assert.Null(update.ObsConnection);
    }

    [Fact]
    public void ToUpdate_WithNullPassword_KeepsTheStoredOne()
    {
        var update = MappingAssert.Succeeds(SettingsMapper.ToUpdate(Update(new ObsUpdateDto("obs.lan", 4456, null)), _storedObs));

        Assert.Equal(new ObsConnection("obs.lan", 4456, "stored-secret"), update.ObsConnection);
    }

    [Fact]
    public void ToUpdate_WithNullPasswordAndNothingStored_HasNoPassword()
    {
        var update = MappingAssert.Succeeds(SettingsMapper.ToUpdate(Update(new ObsUpdateDto("obs.lan", 4456, null)), null));

        Assert.False(update.ObsConnection!.HasPassword);
    }

    [Fact]
    public void ToUpdate_WithEmptyPassword_ClearsIt()
    {
        var update = MappingAssert.Succeeds(SettingsMapper.ToUpdate(Update(new ObsUpdateDto("localhost", 4455, string.Empty)), _storedObs));

        Assert.False(update.ObsConnection!.HasPassword);
    }

    [Fact]
    public void ToUpdate_WithNewPassword_ReplacesIt()
    {
        var update = MappingAssert.Succeeds(SettingsMapper.ToUpdate(Update(new ObsUpdateDto("localhost", 4455, "new")), _storedObs));

        Assert.Equal("new", update.ObsConnection!.Password);
    }

    public static TheoryData<int> InvalidUpdateIndexes() => [.. Enumerable.Range(0, _invalidUpdates.Length)];

    private static readonly SettingsUpdateDto?[] _invalidUpdates =
    [
        null,
        new SettingsUpdateDto(false, null!, null),
        new SettingsUpdateDto(false, new GridDto(2, 2), null),
        Update(new ObsUpdateDto("localhost", 0, null)),
        Update(new ObsUpdateDto("localhost", 70000, null)),
        Update(new ObsUpdateDto(" ", 4455, null)),
        Update(new ObsUpdateDto(null!, 4455, null)),
        Update(new ObsUpdateDto("localhost", 4455, new string('x', 300))),
    ];

    [Theory]
    [MemberData(nameof(InvalidUpdateIndexes))]
    public void InvalidUpdate_BecomesValidationFailure(int index)
    {
        MappingAssert.IsValidationFailure(SettingsMapper.ToUpdate(_invalidUpdates[index]!, _storedObs));
    }

    private static SettingsUpdateDto Update(ObsUpdateDto obs) => new(false, new GridDto(5, 3), obs);
}
