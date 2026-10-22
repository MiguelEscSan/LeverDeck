using System.Net;

namespace LeverDeck.App.Tests;

public sealed class HealthEndpointTests(LeverDeckAppFactory factory) : IClassFixture<LeverDeckAppFactory>
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }
}
