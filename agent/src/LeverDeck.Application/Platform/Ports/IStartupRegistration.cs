namespace LeverDeck.Application.Platform.Ports;

/// <summary>Whether the agent starts with the owner's Windows session (the tray's "Start with Windows" item).</summary>
public interface IStartupRegistration
{
    bool IsEnabled();

    void SetEnabled(bool enabled);
}
