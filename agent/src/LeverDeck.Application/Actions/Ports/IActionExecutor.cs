using LeverDeck.Domain.Common;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Application.Actions.Ports;

/// <summary>
/// Runs an agent-side action on the PC. It only receives actions resolved from a stored profile; a
/// <see cref="Domain.Actions.LaunchApp"/> has already been checked against the registered apps.
/// </summary>
public interface IActionExecutor
{
    Task<Result> ExecuteAsync(DomainAction action, CancellationToken cancellationToken);
}
