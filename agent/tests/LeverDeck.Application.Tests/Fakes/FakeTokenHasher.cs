using LeverDeck.Application.Devices.Ports;
using LeverDeck.Domain.Devices;

namespace LeverDeck.Application.Tests.Fakes;

/// <summary>Deterministic and readable: the hash of <c>abc</c> is <c>hash:abc</c>.</summary>
internal sealed class FakeTokenHasher : ITokenHasher
{
    public TokenHash Hash(string rawToken) => new($"hash:{rawToken}");
}
