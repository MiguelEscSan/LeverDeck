using System.Reflection;
using LeverDeck.Domain.Actions;
using DomainAction = LeverDeck.Domain.Actions.Action;

namespace LeverDeck.Architecture.Tests;

/// <summary>
/// Enforces docs/ARCHITECTURE.md rule 2: actions are a closed, whitelisted set and
/// <see cref="ActionCatalog"/> is the single place that lists them.
/// </summary>
public sealed class ActionWhitelistTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Type[] ConcreteActions =
    [
        .. typeof(DomainAction).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(DomainAction)) && !type.IsAbstract),
    ];

    [Fact]
    public void ConcreteActions_AreSealed()
    {
        var unsealed = ConcreteActions.Where(type => !type.IsSealed).Select(type => type.Name).ToArray();

        Assert.True(unsealed.Length == 0, $"Actions must be sealed: {string.Join(", ", unsealed)}");
    }

    [Fact]
    public void Catalog_ListsExactlyTheConcreteActions()
    {
        var missing = ConcreteActions.Except(ActionCatalog.All).Select(type => type.Name).ToArray();
        var extra = ActionCatalog.All.Except(ConcreteActions).Select(type => type.FullName).ToArray();

        Assert.True(missing.Length == 0, $"Actions missing from ActionCatalog.All: {string.Join(", ", missing)}");
        Assert.True(extra.Length == 0, $"ActionCatalog.All lists non-actions: {string.Join(", ", extra)}");
        Assert.Equal(ActionCatalog.All.Count, ActionCatalog.All.Distinct().Count());
    }

    [Fact]
    public void Action_HasNoPublicConstructor()
    {
        var constructors = typeof(DomainAction).GetConstructors(InstanceMembers);

        Assert.DoesNotContain(constructors, constructor => constructor.IsPublic);
    }

    [Fact]
    public void Action_OnlyProtectedConstructorIsTheRecordCopyConstructor()
    {
        // C# requires the copy constructor of a non-sealed record to be protected; every other one is private protected.
        var protectedConstructors = typeof(DomainAction).GetConstructors(InstanceMembers).Where(constructor => constructor.IsFamily);

        Assert.All(
            protectedConstructors,
            constructor => Assert.Equal([typeof(DomainAction)], constructor.GetParameters().Select(parameter => parameter.ParameterType)));
    }

    [Fact]
    public void Action_DeclaresAnAbstractMemberInvisibleOutsideTheDomain()
    {
        // Another assembly cannot override this member, so it cannot derive from Action even through the copy constructor.
        var sealingMembers = typeof(DomainAction)
            .GetMethods(InstanceMembers)
            .Where(method => method.IsAbstract && (method.IsAssembly || method.IsFamilyAndAssembly));

        Assert.NotEmpty(sealingMembers);
    }
}
