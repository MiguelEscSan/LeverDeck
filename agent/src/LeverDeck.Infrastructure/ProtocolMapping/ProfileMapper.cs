using LeverDeck.Application.Profiles;
using LeverDeck.Domain.Common;
using LeverDeck.Domain.Profiles;
using LeverDeck.Protocol.Dtos;
using static LeverDeck.Infrastructure.ProtocolMapping.EnumMapping;

namespace LeverDeck.Infrastructure.ProtocolMapping;

/// <summary>Maps profiles with their pages, buttons and actions.</summary>
public static class ProfileMapper
{
    public static ProfileDto ToDto(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new ProfileDto(
            profile.Id.Value,
            profile.Name.Value,
            Convert<KeyColor, KeyColorDto>(profile.Color),
            GridMapper.ToDto(profile.GridSize),
            [.. profile.Pages.Select(ToDto)]);
    }

    public static ProfileSummaryDto ToSummaryDto(ProfileSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new ProfileSummaryDto(summary.Id.Value, summary.Name.Value, Convert<KeyColor, KeyColorDto>(summary.Color));
    }

    /// <summary>Rebuilds the profile, validating every domain invariant; an invalid DTO becomes a failure.</summary>
    public static Result<Profile> ToDomain(ProfileDto dto) => MappingBoundary.Run(() =>
    {
        MappingBoundary.Required(dto, "profile");
        return Profile.Restore(
            new ProfileId(dto.Id),
            new ProfileName(dto.Name),
            Convert<KeyColorDto, KeyColor>(dto.Color),
            GridMapper.Create(dto.Grid),
            [.. MappingBoundary.Required(dto.Pages, "pages").Select(ToPage)]);
    });

    private static PageDto ToDto(Page page) => new(page.Index, [.. page.Buttons.Select(ToDto)]);

    private static ButtonDto ToDto(Button button) => new(
        button.Id.Value,
        button.Position.Row,
        button.Position.Column,
        button.Appearance.Label,
        Convert<KeyColor, KeyColorDto>(button.Appearance.Color),
        button.Appearance.Icon.Value,
        ActionMapper.ToDto(button.Action));

    private static Page ToPage(PageDto dto)
    {
        MappingBoundary.Required(dto, "page");
        return new Page(dto.Index, [.. MappingBoundary.Required(dto.Buttons, "buttons").Select(ToButton)]);
    }

    private static Button ToButton(ButtonDto dto)
    {
        MappingBoundary.Required(dto, "button");
        return new Button(
            new ButtonId(dto.Id),
            new GridPosition(dto.Row, dto.Column),
            new ButtonAppearance(dto.Label, Convert<KeyColorDto, KeyColor>(dto.Color), new IconId(dto.IconId)),
            ActionMapper.Create(dto.Action));
    }
}

/// <summary>Maps the allowed grid sizes.</summary>
public static class GridMapper
{
    public static GridDto ToDto(GridSize gridSize)
    {
        ArgumentNullException.ThrowIfNull(gridSize);
        return new GridDto(gridSize.Columns, gridSize.Rows);
    }

    public static Result<GridSize> ToDomain(GridDto dto) => MappingBoundary.Run(() => Create(dto));

    internal static GridSize Create(GridDto dto)
    {
        MappingBoundary.Required(dto, "grid");
        return GridSize.From(dto.Columns, dto.Rows);
    }
}
