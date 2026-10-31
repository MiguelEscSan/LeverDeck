using System.Collections.Concurrent;
using LeverDeck.Application.Common.Ports;
using LeverDeck.Application.Events;
using LeverDeck.Application.Obs;
using LeverDeck.Application.Settings.Ports;
using LeverDeck.Domain.Actions;
using LeverDeck.Domain.Errors;
using LeverDeck.Domain.Profiles;
using LeverDeck.Domain.Settings;
using LeverDeck.Infrastructure.Obs;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeverDeck.Infrastructure.Tests.Obs;

/// <summary>The OBS client against a local stand-in for OBS: the handshake, requests, refusals and events.</summary>
public sealed class ObsWebSocketClientTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private sealed class FixedSettings(AgentSettings settings) : ISettingsRepository
    {
        public Task<AgentSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(settings);

        public Task SaveAsync(AgentSettings settings, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The OBS client never writes settings.");
    }

    private sealed class RecordingEvents : IEventPublisher
    {
        public ConcurrentQueue<ApplicationEvent> Published { get; } = new();

        public Task PublishAsync(ApplicationEvent applicationEvent, CancellationToken cancellationToken)
        {
            Published.Enqueue(applicationEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Maintain_WithThePassword_ConnectsAndPublishesTheOutputStates()
    {
        await using var server = await FakeObsServer.StartAsync("secret");
        var events = new RecordingEvents();
        using var client = Create(server.Port, "secret", events);

        await client.MaintainAsync(CancellationToken.None);

        Assert.True(client.IsConnected);
        Assert.Contains(events.Published, published => published is ObsStateChanged { Streaming: false, Recording: true });
    }

    [Fact]
    public async Task Maintain_WrongPassword_StaysDisconnected()
    {
        await using var server = await FakeObsServer.StartAsync("secret");
        using var client = Create(server.Port, "wrong", new RecordingEvents());

        await client.MaintainAsync(CancellationToken.None);

        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task GetScenes_ReturnsTheNamesInSceneOrder_AndTheCurrentOne()
    {
        await using var server = await FakeObsServer.StartAsync("secret");
        using var client = await ConnectAsync(server, "secret");

        var result = await client.GetScenesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(["Gameplay", "Starting soon"], result.Value.Scenes);
        Assert.Equal("Gameplay", result.Value.CurrentScene);
    }

    [Fact]
    public async Task ControlStream_SendsTheMatchingRequest()
    {
        await using var server = await FakeObsServer.StartAsync("secret");
        using var client = await ConnectAsync(server, "secret");

        var result = await client.ControlStreamAsync(ObsOutputCommand.Toggle, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Contains(server.Requests, request => request.Type == "ToggleStream");
    }

    [Fact]
    public async Task ControlStream_WhenObsRefuses_ReturnsTheComment()
    {
        await using var server = await FakeObsServer.StartAsync("secret");
        using var client = await ConnectAsync(server, "secret");

        var result = await client.ControlStreamAsync(ObsOutputCommand.Start, CancellationToken.None);

        Assert.Equal(DomainErrorCode.ActionFailed, result.Error?.Code);
        Assert.Equal("Already streaming.", result.Error?.Message);
    }

    [Fact]
    public async Task ControlSourceVisibility_Toggle_ReadsTheStateAndFlipsIt()
    {
        await using var server = await FakeObsServer.StartAsync("secret");
        using var client = await ConnectAsync(server, "secret");

        var result = await client.ControlSourceVisibilityAsync("Gameplay", "Webcam", ObsVisibilityCommand.Toggle, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var set = Assert.Single(server.Requests, request => request.Type == "SetSceneItemEnabled");
        Assert.Contains("\"sceneItemEnabled\":false", set.Data, StringComparison.Ordinal);
        Assert.Contains("\"sceneItemId\":7", set.Data, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Command_WhenNotConnected_FailsAtOnceWithoutConnecting()
    {
        await using var server = await FakeObsServer.StartAsync("secret");
        using var client = Create(server.Port, "secret", new RecordingEvents());

        var result = await client.SetSceneAsync("Gameplay", CancellationToken.None);

        Assert.Equal(DomainErrorCode.ActionFailed, result.Error?.Code);
        Assert.Equal("OBS is not connected.", result.Error?.Message);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task ObsEvents_BecomeApplicationEvents()
    {
        await using var server = await FakeObsServer.StartAsync("secret");
        var events = new RecordingEvents();
        using var client = Create(server.Port, "secret", events);
        await client.MaintainAsync(CancellationToken.None);

        await server.PushEventAsync("CurrentProgramSceneChanged", """{"sceneName":"Be right back"}""");

        await WaitUntilAsync(() => events.Published.Any(published => published is ObsSceneChanged { SceneName: "Be right back" }));
    }

    private static ObsWebSocketClient Create(int port, string? password, RecordingEvents events)
    {
        var settings = AgentSettings.CreateDefault();
        settings.ConfigureObs(new ObsConnection("127.0.0.1", port, password));
        return new ObsWebSocketClient(new FixedSettings(settings), events, NullLogger<ObsWebSocketClient>.Instance);
    }

    private static async Task<ObsWebSocketClient> ConnectAsync(FakeObsServer server, string password)
    {
        var client = Create(server.Port, password, new RecordingEvents());
        await client.MaintainAsync(CancellationToken.None);
        Assert.True(client.IsConnected);
        return client;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Patience);
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }
}
