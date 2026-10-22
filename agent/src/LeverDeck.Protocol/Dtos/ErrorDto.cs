namespace LeverDeck.Protocol.Dtos;

/// <summary>Error body of the HTTP endpoints; WebSocket errors use <c>ErrorMessage</c> with the same fields.</summary>
public sealed record ErrorDto(ErrorCodeDto Code, string Message);
