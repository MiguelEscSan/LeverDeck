namespace LeverDeck.App.Tests;

public sealed class StartupSeedingTests
{
    [Fact]
    public void Startup_FirstRun_WritesDefaultProfileToDisk()
    {
        using var factory = new LeverDeckAppFactory();

        // Accessing Services builds and starts the host, which runs the hosted services.
        _ = factory.Services;

        var profiles = Path.Combine(factory.DataDirectory, "profiles.json");
        Assert.True(File.Exists(profiles), "profiles.json must exist right after the agent starts.");
        Assert.Contains("\"Default\"", File.ReadAllText(profiles), StringComparison.Ordinal);
    }
}
