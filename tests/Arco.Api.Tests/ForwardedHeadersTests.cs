using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace Arco.Api.Tests;

/// <summary>Which header the per-IP rate limit trusts for the client address (<c>Proxy:ClientIpHeader</c>).</summary>
public sealed class ForwardedHeadersTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static async Task<HttpStatusCode> LoginAttemptAsync(HttpClient client, params (string Header, string Value)[] headers)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "x@teste.arco", password = "errada-errada" }),
        };
        foreach (var (name, value) in headers) req.Headers.Add(name, value);
        return (await client.SendAsync(req)).StatusCode;
    }

    private static async Task ExhaustAsync(HttpClient client, string header, string ip)
    {
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginAttemptAsync(client, (header, ip)));
    }

    [Fact]
    public async Task Configured_header_ignores_a_forged_x_forwarded_for()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Proxy:ClientIpHeader", "CF-Connecting-IP"));
        var client = factory.CreateClient();

        await ExhaustAsync(client, "CF-Connecting-IP", "198.51.100.1");

        // A different forged X-Forwarded-For does not give a fresh bucket: the real header is still the same IP.
        Assert.Equal(HttpStatusCode.TooManyRequests,
            await LoginAttemptAsync(client, ("CF-Connecting-IP", "198.51.100.1"), ("X-Forwarded-For", "203.0.113.77")));
    }

    [Fact]
    public async Task Configured_header_counts_different_client_ips_separately()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Proxy:ClientIpHeader", "CF-Connecting-IP"));
        var client = factory.CreateClient();

        await ExhaustAsync(client, "CF-Connecting-IP", "198.51.100.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginAttemptAsync(client, ("CF-Connecting-IP", "198.51.100.1")));

        Assert.Equal(HttpStatusCode.Unauthorized, await LoginAttemptAsync(client, ("CF-Connecting-IP", "198.51.100.2")));
    }

    [Fact]
    public async Task Without_the_setting_x_forwarded_for_is_used_and_cf_header_is_ignored()
    {
        var client = _factory.CreateClient();

        await ExhaustAsync(client, "X-Forwarded-For", "198.51.100.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginAttemptAsync(client, ("X-Forwarded-For", "198.51.100.1")));
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginAttemptAsync(client, ("X-Forwarded-For", "198.51.100.2")));

        // CF-Connecting-IP is not trusted by default: it cannot be used to shake off the limit.
        Assert.Equal(HttpStatusCode.TooManyRequests,
            await LoginAttemptAsync(client, ("X-Forwarded-For", "198.51.100.1"), ("CF-Connecting-IP", "203.0.113.9")));
    }
}
