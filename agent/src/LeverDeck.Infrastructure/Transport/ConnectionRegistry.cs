using System.Collections.Concurrent;
using LeverDeck.Application.Security;
using LeverDeck.Domain.Devices;

namespace LeverDeck.Infrastructure.Transport;

/// <summary>
/// Every authenticated WebSocket connection, by scope and device. A connection enters only after it is bound to its
/// caller (a device after <c>connectDevice</c>, the editor once its token is checked) and leaves when its session ends.
/// </summary>
public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<WebSocketConnection, byte> _connections = new();

    public void Add(WebSocketConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.Caller.Scope == CallerScope.Public)
        {
            throw new InvalidOperationException("Only authenticated connections are registered.");
        }

        _connections.TryAdd(connection, 0);
    }

    public void Remove(WebSocketConnection connection) => _connections.TryRemove(connection, out _);

    public IReadOnlyList<WebSocketConnection> All() => [.. _connections.Keys];

    public IReadOnlyList<WebSocketConnection> Admins() =>
        [.. _connections.Keys.Where(connection => connection.Caller.Scope == CallerScope.Admin)];

    public IReadOnlyList<WebSocketConnection> OfDevice(DeviceId deviceId) =>
        [.. _connections.Keys.Where(connection => connection.Caller.DeviceId == deviceId)];

    public bool IsConnected(DeviceId deviceId) =>
        _connections.Keys.Any(connection => connection.Caller.DeviceId == deviceId && !connection.IsClosing);
}
