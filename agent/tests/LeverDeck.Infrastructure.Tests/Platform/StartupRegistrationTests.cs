using LeverDeck.Infrastructure.Platform;

namespace LeverDeck.Infrastructure.Tests.Platform;

public sealed class StartupRegistrationTests
{
    [Fact]
    public void CommandQuotesThePathOfThePublishedExecutableAndStartsItInTheBackground()
    {
        var command = RunKeyStartupRegistration.CommandFor(@"C:\Program Files\LeverDeck\leverdeck.exe");

        Assert.Equal(@"""C:\Program Files\LeverDeck\leverdeck.exe"" --background", command);
    }

    [Theory]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData(@"C:\Projects\LeverDeck\agent\src\LeverDeck.App\bin\Debug\leverdeck.dll")]
    public void CommandRefusesAnythingButThePublishedExecutable(string path)
    {
        Assert.Throws<InvalidOperationException>(() => RunKeyStartupRegistration.CommandFor(path));
    }
}
