using System.Globalization;
using LeverDeck.Application.Pairing.Ports;
using LeverDeck.Domain.Pairing;

namespace LeverDeck.Application.Tests.Fakes;

/// <summary>Deterministic: tokens are <c>token-1</c>, <c>token-2</c>…; codes are <c>100001</c>, <c>100002</c>….</summary>
internal sealed class FakeTokenGenerator : ITokenGenerator
{
    private int _tokens;
    private int _codes;

    public string NewDeviceToken() => $"token-{++_tokens}";

    public PairingCode NewPairingCode() => new((100000 + ++_codes).ToString(CultureInfo.InvariantCulture));
}
