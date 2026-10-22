using System.Runtime.CompilerServices;
using LeverDeck.Domain.Actions;
using LeverDeck.Infrastructure.Input;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Architecture.Tests;

/// <summary>
/// The executor half of docs/ARCHITECTURE.md rule 2 (deferred from ROADMAP 2.4): every whitelisted action is
/// classified by the executor routing, so adding an action without deciding how it runs fails the build.
/// </summary>
public sealed class ExecutorCoverageTests
{
    [Fact]
    public void Coverage_ClassifiesExactlyTheCatalog()
    {
        var unclassified = ActionCatalog.All.Except(ActionExecutorCoverage.ByActionType.Keys).Select(type => type.Name).ToArray();
        var unknown = ActionExecutorCoverage.ByActionType.Keys.Except(ActionCatalog.All).Select(type => type.FullName).ToArray();

        Assert.True(unclassified.Length == 0, $"Actions without an executor classification: {string.Join(", ", unclassified)}");
        Assert.True(unknown.Length == 0, $"Executor classifies types outside ActionCatalog.All: {string.Join(", ", unknown)}");
    }

    [Fact]
    public void Coverage_UsesOnlyDefinedSupportValues()
    {
        Assert.All(ActionExecutorCoverage.ByActionType.Values, support => Assert.True(Enum.IsDefined(support)));
    }

    [Fact]
    public void ClientOnly_MatchesTheActionsThatRunOnTheClient()
    {
        // Target is a per-type constant, so an uninitialized instance is enough to read it.
        Assert.All(ActionCatalog.All, type =>
        {
            var target = ((DomainAction)RuntimeHelpers.GetUninitializedObject(type)).Target;
            var support = ActionExecutorCoverage.ByActionType[type];

            Assert.True(
                (target == ExecutionTarget.Client) == (support == ActionSupport.ClientOnly),
                $"{type.Name} runs on {target} but the executor classifies it as {support}.");
        });
    }
}
