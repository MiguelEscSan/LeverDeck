using LeverDeck.Application;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Infrastructure.Configuration;
using LeverDeck.Infrastructure.Discovery;
using LeverDeck.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace LeverDeck.App.Tests;

public sealed class CompositionRootTests(DevelopmentAppFactory factory) : IClassFixture<DevelopmentAppFactory>
{
    // A use case is a public sealed class of the Application layer with a public ExecuteAsync (docs/ARCHITECTURE.md).
    private static readonly Type[] UseCases =
    [
        .. typeof(ApplicationInfo).Assembly.GetExportedTypes()
            .Where(type => type is { IsClass: true, IsSealed: true, IsAbstract: false } && type.GetMethod("ExecuteAsync") is not null),
    ];

    public static TheoryData<string> UseCaseNames => [.. UseCases.Select(type => type.FullName!)];

    [Fact]
    public void Application_Has18UseCases() => Assert.Equal(18, UseCases.Length);

    [Theory]
    [MemberData(nameof(UseCaseNames))]
    public void EveryUseCase_Resolves(string useCase)
    {
        // Development turns on ValidateOnBuild and ValidateScopes, so a missing adapter already fails the host build.
        var type = UseCases.Single(candidate => candidate.FullName == useCase);

        Assert.NotNull(factory.Services.GetRequiredService(type));
    }

    [Fact]
    public void Options_ComeFromTheTestConfiguration()
    {
        var options = factory.Services.GetRequiredService<LeverDeckOptions>();

        Assert.Equal(factory.DataDirectory, options.DataDirectory);
        Assert.False(options.MdnsEnabled);
    }

    [Fact]
    public void Advertiser_AnswersForTheHostNameAndRegistersTheService()
    {
        var advertiser = Assert.IsType<CompositeServiceAdvertiser>(factory.Services.GetRequiredService<IServiceAdvertiser>());

        Assert.Collection(
            advertiser.Advertisers,
            first => Assert.IsType<MdnsHostNameResponder>(first),
            second => Assert.IsType<DnsServiceAdvertiser>(second));
    }

    [Fact]
    public void AdminToken_IsCreatedInTheTestDataDirectory()
    {
        _ = factory.Server; // starts the hosted services

        var store = factory.Services.GetRequiredService<AdminTokenStore>();

        Assert.StartsWith(factory.DataDirectory, store.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(store.FilePath));
    }
}
