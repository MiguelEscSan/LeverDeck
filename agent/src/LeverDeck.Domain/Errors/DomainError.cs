namespace LeverDeck.Domain.Errors;

/// <summary>An expected business failure, carried by a <see cref="Common.Result"/>.</summary>
public sealed record DomainError(DomainErrorCode Code, string Message);
