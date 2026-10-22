using LeverDeck.Application.Actions.Ports;
using LeverDeck.Domain.Common;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Application.Tests.Fakes;

/// <summary>Records every action it is asked to run and answers with <see cref="NextResult"/>.</summary>
internal sealed class RecordingActionExecutor : IActionExecutor
{
    private readonly List<DomainAction> _executed = [];

    public IReadOnlyList<DomainAction> Executed => _executed;

    public Result NextResult { get; set; } = Result.Success();

    public Task<Result> ExecuteAsync(DomainAction action, CancellationToken cancellationToken)
    {
        _executed.Add(action);
        return Task.FromResult(NextResult);
    }
}
