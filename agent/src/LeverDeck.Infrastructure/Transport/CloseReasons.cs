using System.Text.Json;
using LeverDeck.Protocol.Dtos;

namespace LeverDeck.Infrastructure.Transport;

/// <summary>
/// The close reason the agent sends is the wire error code (<c>unauthorized</c>, <c>deviceRevoked</c>, …), so a
/// client can tell "pair again" from "reconnect later" even when it missed the preceding <c>error</c> or event.
/// </summary>
public static class CloseReasons
{
    public const string Shutdown = "shutdown";

    public static string For(ErrorCodeDto code) => JsonNamingPolicy.CamelCase.ConvertName(code.ToString());
}
