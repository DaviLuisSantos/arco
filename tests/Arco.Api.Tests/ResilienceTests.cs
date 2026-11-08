using System.Net;
using Arco.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;

namespace Arco.Api.Tests;

public sealed class ResilienceTests
{
    private sealed class CountingHandler(Func<HttpRequestMessage, HttpStatusCode> respond) : HttpMessageHandler
    {
        private int _calls;
        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new HttpResponseMessage(respond(request)));
        }
    }

    private static HttpClient CreateClient(CountingHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient("t").ConfigurePrimaryHttpMessageHandler(() => handler).AddArcoResilience(TimeSpan.Zero);
        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("t");
    }

    [Fact]
    public async Task Gone_is_called_once_and_never_retried()
    {
        var handler = new CountingHandler(_ => HttpStatusCode.Gone);
        var http = CreateClient(handler);

        for (var i = 0; i < 20; i++)
        {
            using var res = await http.PostAsync("https://push.example/a", new StringContent("x"));
            Assert.Equal(HttpStatusCode.Gone, res.StatusCode);
        }

        // One call per request, and 410 never opens the circuit.
        Assert.Equal(20, handler.Calls);
    }

    [Fact]
    public async Task Server_errors_are_retried_then_open_the_circuit()
    {
        var handler = new CountingHandler(_ => HttpStatusCode.InternalServerError);
        var http = CreateClient(handler);

        using (var first = await http.PostAsync("https://push.example/a", new StringContent("x")))
            Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);
        Assert.Equal(4, handler.Calls); // 1 attempt + 3 retries

        await Assert.ThrowsAsync<BrokenCircuitException>(async () =>
        {
            for (var i = 0; i < 10; i++)
            {
                using var res = await http.PostAsync("https://push.example/a", new StringContent("x"));
            }
        });
    }

    [Fact]
    public async Task One_host_failing_does_not_open_the_circuit_of_another()
    {
        var handler = new CountingHandler(r => r.RequestUri!.Host == "bad.example"
            ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Created);
        var http = CreateClient(handler);

        await Assert.ThrowsAsync<BrokenCircuitException>(async () =>
        {
            for (var i = 0; i < 10; i++)
            {
                using var res = await http.PostAsync("https://bad.example/a", new StringContent("x"));
            }
        });

        using var ok = await http.PostAsync("https://good.example/a", new StringContent("x"));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }
}
