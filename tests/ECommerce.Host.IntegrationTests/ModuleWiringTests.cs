using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ECommerce.Host.IntegrationTests;

public class ModuleWiringTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ModuleWiringTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/users/ping")]
    [InlineData("/api/products/ping")]
    [InlineData("/api/orders/ping")]
    [InlineData("/api/notifications/ping")]
    public async Task Endpoint_returns_200_OK(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}