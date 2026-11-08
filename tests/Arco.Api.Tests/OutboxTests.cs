using System.Net.Http.Json;
using Arco.Api.Domain;
using Arco.Api.Infrastructure;
using Arco.Api.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Arco.Api.Tests;

public sealed class OutboxTests : IDisposable
{
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private async Task<T> InScopeAsync<T>(Func<AppDbContext, OutboxProcessor, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<OutboxProcessor>());
    }

    private Task<List<OutboxMessage>> MessagesAsync() =>
        InScopeAsync((db, _) => db.OutboxMessages.AsNoTracking().OrderBy(m => m.Id).ToListAsync());

    private Task<int> RunOnceAsync() => InScopeAsync((_, p) => p.RunOnceAsync(CancellationToken.None));

    private async Task<string> RegisterAsync()
    {
        var email = $"outbox-{Guid.NewGuid():N}@teste.arco";
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password = ApiFactory.DefaultPassword });
        Assert.Equal(System.Net.HttpStatusCode.Created, res.StatusCode);
        return email;
    }

    private async Task<long> EnqueueEmailAsync(string to = "alguem@teste.arco")
    {
        var now = _factory.Time!.Now.UtcDateTime;
        return await InScopeAsync(async (db, _) =>
        {
            var m = Outbox.Enqueue(db, OutboxKinds.Email, new EmailOutboxPayload(to, "Assunto", "<p>token=abc</p>"), now);
            await db.SaveChangesAsync();
            return m.Id;
        });
    }

    [Fact]
    public async Task Successful_send_happens_right_away_and_clears_the_payload()
    {
        var email = await RegisterAsync();

        Assert.Contains(_factory.Email.Sent, m => m.To == email);
        var m = Assert.Single(await MessagesAsync());
        Assert.NotNull(m.SentAt);
        Assert.Equal("", m.Payload);
        Assert.Equal(1, m.Attempts);
        Assert.Equal($"outbox-{m.Id}", Assert.Single(_factory.Email.Keys));
    }

    [Fact]
    public async Task Failure_does_not_break_the_request_and_is_retried_by_the_worker()
    {
        _factory.Email.FailNext(1);
        var email = await RegisterAsync();

        Assert.Empty(_factory.Email.Sent);
        var failed = Assert.Single(await MessagesAsync());
        Assert.Null(failed.SentAt);
        Assert.Equal(1, failed.Attempts);
        Assert.False(string.IsNullOrEmpty(failed.LastError));
        Assert.Contains("token=", failed.Payload); // still needed for the retry

        // Not due yet.
        Assert.Equal(0, await RunOnceAsync());
        _factory.Time!.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await RunOnceAsync());

        Assert.Equal(email, Assert.Single(_factory.Email.Sent).To);
        var sent = Assert.Single(await MessagesAsync());
        Assert.NotNull(sent.SentAt);
        Assert.Null(sent.LastError);
        Assert.Equal("", sent.Payload);
        Assert.Equal(2, sent.Attempts);
    }

    [Fact]
    public async Task Backoff_grows_and_the_message_is_given_up_after_six_attempts()
    {
        _factory.Email.FailNext(int.MaxValue);
        await EnqueueEmailAsync();
        var id = (await MessagesAsync()).Single().Id;
        await InScopeAsync(async (_, p) => { await p.TryDispatchAsync(id, CancellationToken.None); return 0; });

        TimeSpan[] waits = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(12)];
        for (var i = 0; i < waits.Length; i++)
        {
            var m = Assert.Single(await MessagesAsync());
            Assert.Equal(i + 1, m.Attempts);
            Assert.Equal(_factory.Time!.Now.UtcDateTime + waits[i], m.NextAttemptAt);

            _factory.Time.Advance(waits[i] - TimeSpan.FromSeconds(1));
            Assert.Equal(0, await RunOnceAsync()); // one second early
            _factory.Time.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(1, await RunOnceAsync());
        }

        var last = Assert.Single(await MessagesAsync());
        Assert.Equal(6, last.Attempts);
        Assert.NotNull(last.FailedAt);
        Assert.Null(last.SentAt);
        Assert.Equal("", last.Payload);

        // Given up: never tried again.
        _factory.Time!.AdvanceDays(1);
        Assert.Equal(0, await RunOnceAsync());
        Assert.Empty(_factory.Email.Sent);
    }

    [Fact]
    public async Task Concurrent_dispatches_send_only_once()
    {
        _factory.Email.Delay = TimeSpan.FromMilliseconds(300);
        var id = await EnqueueEmailAsync();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            InScopeAsync(async (_, p) => { await p.TryDispatchAsync(id, CancellationToken.None); return 0; })));
        await RunOnceAsync();

        Assert.Single(_factory.Email.Sent);
        Assert.NotNull(Assert.Single(await MessagesAsync()).SentAt);
    }

    [Fact]
    public async Task A_claimed_message_is_skipped_until_the_lock_expires()
    {
        var id = await EnqueueEmailAsync();
        var now = _factory.Time!.Now.UtcDateTime;
        await InScopeAsync(async (db, _) =>
        {
            await db.OutboxMessages.Where(m => m.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntil, now + OutboxProcessor.LockDuration));
            return 0;
        });

        Assert.Equal(0, await RunOnceAsync());
        _factory.Time.Advance(OutboxProcessor.LockDuration + TimeSpan.FromSeconds(1));
        Assert.Equal(1, await RunOnceAsync());
        Assert.Single(_factory.Email.Sent);
    }

    [Fact]
    public async Task Old_sent_messages_are_deleted_after_30_days()
    {
        var id = await EnqueueEmailAsync();
        await InScopeAsync(async (_, p) => { await p.TryDispatchAsync(id, CancellationToken.None); return 0; });
        Assert.Single(await MessagesAsync());

        _factory.Time!.AdvanceDays(29);
        await RunOnceAsync();
        Assert.Single(await MessagesAsync());

        _factory.Time.AdvanceDays(2);
        await RunOnceAsync();
        Assert.Empty(await MessagesAsync());
    }
}
