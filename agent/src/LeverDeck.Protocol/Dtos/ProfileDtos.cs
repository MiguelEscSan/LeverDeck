namespace LeverDeck.Protocol.Dtos;

public sealed record GridDto(int Columns, int Rows);

public sealed record ProfileSummaryDto(Guid Id, string Name, KeyColorDto Color);

public sealed record ProfileDto(Guid Id, string Name, KeyColorDto Color, GridDto Grid, IReadOnlyList<PageDto> Pages);

public sealed record PageDto(int Index, IReadOnlyList<ButtonDto> Buttons);

public sealed record ButtonDto(
    Guid Id,
    int Row,
    int Column,
    string Label,
    KeyColorDto Color,
    string IconId,
    ActionDto Action);
