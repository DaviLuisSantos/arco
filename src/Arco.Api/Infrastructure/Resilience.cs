using Microsoft.Extensions.Http.Resilience;

namespace Arco.Api.Infrastructure;

public static class ResilienceExtensions
{
    /// <summary>
    /// Standard resilience for calls to third parties (Resend, push services): retries with backoff and jitter, a
    /// circuit breaker and timeouts, with one pipeline per authority so one failing host does not open the circuit of
    /// another. 404/410 are not retried and do not count as failures (the standard predicate only handles 5xx, 408 and
    /// network errors). Retrying POST is allowed: Resend takes an Idempotency-Key and a push is harmless to repeat.
    /// Do not also set <c>HttpClient.Timeout</c>: it would cut the retries short; <c>TotalRequestTimeout</c> takes over.
    /// </summary>
    /// <param name="baseDelay">Delay of the first retry (default 2 s); tests pass <see cref="TimeSpan.Zero"/>.</param>
    public static IHttpClientBuilder AddArcoResilience(this IHttpClientBuilder builder, TimeSpan? baseDelay = null)
    {
        builder.AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(25);
                o.Retry.MaxRetryAttempts = 3;
                o.Retry.Delay = baseDelay ?? TimeSpan.FromSeconds(2);
                // The defaults (100 calls in 30 s) would never open for this app's traffic.
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60); // must be at least twice the attempt timeout
                o.CircuitBreaker.MinimumThroughput = 8;
                o.CircuitBreaker.FailureRatio = 0.5;
                o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            })
            .SelectPipelineByAuthority();
        return builder;
    }
}
