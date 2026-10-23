using LeverDeck.App.Desktop;
using LeverDeck.Application.Platform.Ports;

namespace LeverDeck.App.Tests.Desktop;

public sealed class InstallerTests
{
    private sealed class FakeStartup(Exception? failure = null) : IStartupRegistration
    {
        public bool Enabled { get; private set; }

        public bool IsEnabled() => Enabled;

        public void SetEnabled(bool enabled)
        {
            if (failure is not null)
            {
                throw failure;
            }

            Enabled = enabled;
        }
    }

    [Fact]
    public void AfterInstall_StartsLeverDeckWithWindows()
    {
        var startup = new FakeStartup();

        Installer.AfterInstall(startup);

        Assert.True(startup.Enabled);
    }

    [Fact]
    public void BeforeUninstall_StopsStartingWithWindows()
    {
        var startup = new FakeStartup();
        startup.SetEnabled(true);

        Installer.BeforeUninstall(startup);

        Assert.False(startup.Enabled);
    }

    [Fact]
    public void AfterInstall_KeepsInstallingWhenTheRunKeyCannotBeWritten()
    {
        var startup = new FakeStartup(new UnauthorizedAccessException("Run key is locked."));

        Installer.AfterInstall(startup);

        Assert.False(startup.Enabled);
    }
}
