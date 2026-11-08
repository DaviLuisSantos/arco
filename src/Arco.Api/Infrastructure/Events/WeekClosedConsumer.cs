using System.Net;
using Arco.Api.Domain;
using Arco.Api.Infrastructure.Outbox;
using Arco.Api.Infrastructure.Push;
using Microsoft.EntityFrameworkCore;
using OutboxHelper = Arco.Api.Infrastructure.Outbox.Outbox;

namespace Arco.Api.Infrastructure.Events;

/// <summary>
/// Tells each accepted partner of the user that the week was closed: a push if the partner has a subscription,
/// otherwise (or if no device took it) an e-mail through the outbox. Each partner is handled in a scope acting as
/// them and claimed in PushDispatches with <c>week-closed:{owner}:{weekStart}</c>, so a redelivered event never notifies twice.
/// </summary>
public sealed class WeekClosedConsumer(AppDbContext db, IServiceScopeFactory scopes, IConfiguration config,
    ILogger<WeekClosedConsumer> log)
{
    public async Task HandleAsync(WeekClosed e, CancellationToken ct)
    {
        var partners = await db.Partnerships.AsNoTracking()
            .Where(p => p.OwnerUserId == e.OwnerUserId && p.PartnerUserId != null && p.AcceptedAt != null)
            .Select(p => new { p.Id, PartnerId = p.PartnerUserId!.Value })
            .ToListAsync(ct);
        if (partners.Count == 0) return;

        var ownerName = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == e.OwnerUserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
        if (ownerName is null) return;

        var failures = new List<Exception>();
        foreach (var partner in partners)
        {
            try
            {
                using var scope = scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<CurrentUser>().Impersonate(partner.PartnerId);
                await NotifyAsync(scope.ServiceProvider, e, partner.Id, partner.PartnerId, ownerName, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Aviso de semana fechada falhou para o parceiro {PartnerId}", partner.PartnerId);
                failures.Add(ex);
            }
        }
        // Rethrow so the event is redelivered; partners already notified are skipped by their claim.
        if (failures.Count > 0) throw new AggregateException(failures);
    }

    private async Task NotifyAsync(IServiceProvider sp, WeekClosed e, int partnershipId, int partnerId, string ownerName,
        CancellationToken ct)
    {
        var pdb = sp.GetRequiredService<AppDbContext>();
        var time = sp.GetRequiredService<TimeProvider>();

        var key = $"week-closed:{e.OwnerUserId}:{e.WeekStart:yyyy-MM-dd}";
        if (await pdb.PushDispatches.AnyAsync(d => d.Key == key, ct)) return;

        var partner = await pdb.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.Id == partnerId)
            .Select(u => new { u.Email, u.Locale }).FirstOrDefaultAsync(ct);
        if (partner is null) return;

        var en = partner.Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var path = $"/partners/{partnershipId}";
        var (title, body) = en
            ? ($"{ownerName} closed the week", $"See how the week of {e.WeekStart:MM/dd} went.")
            : ($"{ownerName} fechou a semana", $"Veja como foi a semana de {e.WeekStart:dd/MM}.");

        var hasPush = await pdb.PushSubscriptions.AnyAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        OutboxMessage? mail = null;

        // The claim and (without push) the e-mail are saved together.
        pdb.PushDispatches.Add(new PushDispatch { Key = key, SentAt = now });
        if (!hasPush) mail = EnqueueMail(pdb, partner.Email, title, body, path, en, now);
        try
        {
            await pdb.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return; // another delivery claimed it first
        }

        if (hasPush)
        {
            var sent = await sp.GetRequiredService<PushNotifier>()
                .SendToAllAsync(new PushMessage(title, body, path, $"week-closed-{e.OwnerUserId}"), ct);
            if (sent == 0)
            {
                mail = EnqueueMail(pdb, partner.Email, title, body, path, en, now);
                await pdb.SaveChangesAsync(ct);
            }
        }

        if (mail is not null) await sp.GetRequiredService<OutboxProcessor>().TryDispatchAsync(mail.Id, ct);
    }

    private OutboxMessage EnqueueMail(AppDbContext pdb, string to, string title, string body, string path, bool en, DateTime now)
    {
        var link = $"{(config["App:BaseUrl"] ?? "").TrimEnd('/')}{path}";
        return OutboxHelper.Enqueue(pdb, OutboxKinds.Email, new EmailOutboxPayload(to, title, Html(title, body, link, en)), now);
    }

    private static string Html(string title, string body, string link, bool en)
    {
        var l = WebUtility.HtmlEncode(link);
        var lang = en ? "en" : "pt-BR";
        var button = en ? "View summary" : "Ver resumo";
        return $"""
<div lang="{lang}" style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#1c1917">
  <h1 style="font-size:20px;margin:0 0 16px">Arco</h1>
  <h2 style="font-size:18px;margin:0 0 12px">{WebUtility.HtmlEncode(title)}</h2>
  <p style="line-height:1.5">{WebUtility.HtmlEncode(body)}</p>
  <p style="margin:24px 0"><a href="{l}" style="background:#1c1917;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none">{button}</a></p>
</div>
""";
    }
}

public static class EventsServiceCollectionExtensions
{
    public static IServiceCollection AddArcoEvents(this IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<WeekClosedConsumer>();
        s.AddScoped<IOutboxHandler, WeekClosedOutboxHandler>();

        var connectionString = c["ServiceBus:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            s.AddScoped<IEventBus, InMemoryEventBus>();
        }
        else
        {
            s.AddSingleton(new Azure.Messaging.ServiceBus.ServiceBusClient(connectionString));
            s.AddSingleton<IEventBus, ServiceBusEventBus>();
            s.AddHostedService<ServiceBusConsumerService>();
        }
        return s;
    }
}
