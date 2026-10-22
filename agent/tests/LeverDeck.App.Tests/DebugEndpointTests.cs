using System.Net;
using System.Text;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LeverDeck.App.Tests;

/// <summary>
/// The debug endpoint can inject real input, so these tests only inspect the route table, and the single request
/// they send goes to a Production host, where the route must not exist.
/// </summary>
public sealed class DebugEndpointTests
{
    private const string Path = "/debug/execute";

    private static string[] Routes(LeverDeckAppFactory factory)
    {
        _ = factory.Server; // builds and starts the host
        return
        [
            .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty),
        ];
    }

    [Fact]
    public void Production_HasNoDebugRoute()
    {
        using var factory = new LeverDeckAppFactory();

        Assert.DoesNotContain(Path, Routes(factory));
    }

    [Fact]
    public async Task Production_AnswersNotFound()
    {
        using var factory = new LeverDeckAppFactory();
        using var client = factory.CreateClient();
        using var body = new StringContent(
            $$"""{"profileId":"{{Guid.NewGuid()}}","pageIndex":0,"buttonId":"{{Guid.NewGuid()}}"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(new Uri(Path, UriKind.Relative), body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Development_MapsTheDebugRoute()
    {
        using var factory = new DevelopmentAppFactory();

        Assert.Contains(Path, Routes(factory));
    }
}
