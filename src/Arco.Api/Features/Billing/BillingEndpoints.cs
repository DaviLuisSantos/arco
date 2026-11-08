using System.Globalization;
using System.Text.Json;
using Stripe;

namespace Arco.Api.Features.Billing;

/// <summary>Builds the Stripe client for a secret key. Registered in DI so tests can swap the HTTP transport.</summary>
public delegate IStripeClient StripeClientFactory(string secretKey);

public static class BillingEndpoints
{
    public sealed record PlanLimitsDto(int? Habits, int? ApiKeys, bool Mcp, bool ProfileTheme);

    public sealed record PlanUsageDto(int Habits, int ApiKeys);

    public sealed record BillingDto(
        string Plan, DateTime? RenewsAt, bool CancelAtPeriodEnd, bool StripeEnabled, PlanLimitsDto Limits, PlanUsageDto Usage);

    public sealed record UrlDto(string Url);

    /// <param name="secured">Rotas autenticadas (<c>/api</c>).</param>
    /// <param name="anon">Rotas anônimas (<c>/api</c>), como o webhook do Stripe.</param>
    public static RouteGroupBuilder MapBilling(this RouteGroupBuilder secured, RouteGroupBuilder anon)
    {
        var group = secured.MapGroup("/billing").WithTags("Plano");
        group.MapGet("", Get).WithSummary("Plano atual, limites e uso")
            .Produces<BillingDto>();
        group.MapPost("/checkout", Checkout).WithSummary("Cria a sessão de checkout do Stripe (assinatura Pro)")
            .Produces<UrlDto>().ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/portal", Portal).WithSummary("Abre o portal de cobrança do Stripe")
            .Produces<UrlDto>().ProducesProblem(StatusCodes.Status404NotFound);

        anon.MapPost("/billing/webhook", Webhook).WithTags("Plano").WithSummary("Webhook do Stripe (assinado, idempotente)")
            .AllowAnonymous().Produces(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status400BadRequest);
        return secured;
    }

    private sealed record StripeSettings(string? SecretKey, string? PriceId, string? WebhookSecret)
    {
        public bool Enabled => !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(PriceId);
        public static StripeSettings From(IConfiguration c) =>
            new(c["Stripe:SecretKey"], c["Stripe:PriceId"], c["Stripe:WebhookSecret"]);
    }

    private static IResult Disabled() =>
        ApiResults.Error(StatusCodes.Status404NotFound, ErrorCodes.StripeDisabled, "Pagamentos não estão configurados neste servidor.");

    private static string BaseUrl(IConfiguration config, HttpContext http) =>
        (config["App:BaseUrl"] ?? $"{http.Request.Scheme}://{http.Request.Host}").TrimEnd('/');

    private static async Task<BillingDto> Get(AppDbContext db, CurrentUser current, IConfiguration config, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == current.Id, ct);
        var plan = user.Plan;
        return new BillingDto(
            plan == UserPlan.Pro ? "pro" : "free",
            user.PlanRenewsAt,
            user.PlanCancelAtPeriodEnd,
            StripeSettings.From(config).Enabled,
            new PlanLimitsDto(PlanLimits.MaxActiveHabits(plan), PlanLimits.MaxApiKeys(plan), PlanLimits.Mcp(plan), PlanLimits.ProfileTheme(plan)),
            new PlanUsageDto(await db.Habits.CountAsync(h => h.ArchivedAt == null, ct), await db.ApiKeys.CountAsync(ct)));
    }

    private static async Task<IResult> Checkout(AppDbContext db, CurrentUser current, IConfiguration config, HttpContext http,
        StripeClientFactory stripe, CancellationToken ct)
    {
        var settings = StripeSettings.From(config);
        if (!settings.Enabled) return Disabled();

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == current.Id, ct);
        if (user.Plan == UserPlan.Pro && user.StripeSubscriptionId is not null)
            return ApiResults.Conflict("Você já tem uma assinatura Pro. Use o portal para gerenciá-la.");

        var baseUrl = BaseUrl(config, http);
        var userId = user.Id.ToString(CultureInfo.InvariantCulture);
        var options = new Stripe.Checkout.SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = [new Stripe.Checkout.SessionLineItemOptions { Price = settings.PriceId, Quantity = 1 }],
            ClientReferenceId = userId,
            SuccessUrl = $"{baseUrl}/profile?checkout=success",
            CancelUrl = $"{baseUrl}/profile?checkout=cancel",
            SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { ["userId"] = userId },
            },
        };
        if (user.StripeCustomerId is { } customer) options.Customer = customer;
        else options.CustomerEmail = user.Email;

        try
        {
            var session = await new Stripe.Checkout.SessionService(stripe(settings.SecretKey!))
                .CreateAsync(options, cancellationToken: ct);
            return Results.Ok(new UrlDto(session.Url));
        }
        catch (StripeException)
        {
            return ApiResults.Error(StatusCodes.Status502BadGateway, ErrorCodes.InternalError,
                "Não foi possível falar com o Stripe. Tente novamente.");
        }
    }

    private static async Task<IResult> Portal(AppDbContext db, CurrentUser current, IConfiguration config, HttpContext http,
        StripeClientFactory stripe, CancellationToken ct)
    {
        var settings = StripeSettings.From(config);
        if (!settings.Enabled) return Disabled();

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == current.Id, ct);
        if (user.StripeCustomerId is null) return ApiResults.NotFound("Você ainda não tem uma assinatura.");

        try
        {
            var session = await new Stripe.BillingPortal.SessionService(stripe(settings.SecretKey!)).CreateAsync(
                new Stripe.BillingPortal.SessionCreateOptions
                {
                    Customer = user.StripeCustomerId,
                    ReturnUrl = $"{BaseUrl(config, http)}/profile",
                }, cancellationToken: ct);
            return Results.Ok(new UrlDto(session.Url));
        }
        catch (StripeException)
        {
            return ApiResults.Error(StatusCodes.Status502BadGateway, ErrorCodes.InternalError,
                "Não foi possível falar com o Stripe. Tente novamente.");
        }
    }

    private static async Task<IResult> Webhook(HttpContext http, AppDbContext db, IConfiguration config, TimeProvider time,
        ILoggerFactory logs, CancellationToken ct)
    {
        var secret = StripeSettings.From(config).WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret)) return Disabled();

        string payload;
        using (var reader = new StreamReader(http.Request.Body)) payload = await reader.ReadToEndAsync(ct);

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, http.Request.Headers["Stripe-Signature"].ToString(), secret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return ApiResults.Validation("Assinatura do webhook inválida.");
        }

        if (await db.StripeEvents.AnyAsync(e => e.Id == stripeEvent.Id, ct)) return Results.Ok();

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("object", out var obj))
        {
            var now = time.GetUtcNow().UtcDateTime;
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    await OnCheckoutCompletedAsync(db, obj, ct);
                    break;
                case "customer.subscription.updated":
                    await OnSubscriptionChangedAsync(db, obj, deleted: false, ct);
                    break;
                case "customer.subscription.deleted":
                    await OnSubscriptionChangedAsync(db, obj, deleted: true, ct);
                    break;
            }
            db.StripeEvents.Add(new StripeEvent { Id = stripeEvent.Id, ProcessedAt = now });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // The same event was delivered twice at once; the other request applied it.
            logs.CreateLogger("Billing").LogInformation("Stripe event {EventId} already processed", stripeEvent.Id);
        }
        return Results.Ok();
    }

    private static async Task OnCheckoutCompletedAsync(AppDbContext db, JsonElement session, CancellationToken ct)
    {
        var reference = Str(session, "client_reference_id");
        if (!int.TryParse(reference, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)) return;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;

        user.Plan = UserPlan.Pro;
        user.StripeCustomerId = Str(session, "customer") ?? user.StripeCustomerId;
        user.StripeSubscriptionId = Str(session, "subscription") ?? user.StripeSubscriptionId;
        user.PlanCancelAtPeriodEnd = false;
    }

    private static async Task OnSubscriptionChangedAsync(AppDbContext db, JsonElement sub, bool deleted, CancellationToken ct)
    {
        var customer = Str(sub, "customer");
        var subscriptionId = Str(sub, "id");
        var user = customer is null ? null : await db.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == customer, ct);
        user ??= subscriptionId is null ? null : await db.Users.FirstOrDefaultAsync(u => u.StripeSubscriptionId == subscriptionId, ct);
        if (user is null && sub.TryGetProperty("metadata", out var meta)
            && int.TryParse(Str(meta, "userId"), NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;

        // An old subscription ending must not downgrade a user who already has a newer one.
        if (user.StripeSubscriptionId is { } current && subscriptionId is not null && current != subscriptionId && deleted) return;

        var status = Str(sub, "status");
        var active = !deleted && status is "active" or "trialing" or "past_due";
        user.StripeCustomerId = customer ?? user.StripeCustomerId;
        if (active)
        {
            user.Plan = UserPlan.Pro;
            user.StripeSubscriptionId = subscriptionId ?? user.StripeSubscriptionId;
            user.PlanRenewsAt = PeriodEnd(sub) ?? user.PlanRenewsAt;
            // The customer portal on current API versions sets cancel_at and leaves cancel_at_period_end false.
            user.PlanCancelAtPeriodEnd = (sub.TryGetProperty("cancel_at_period_end", out var c) && c.ValueKind == JsonValueKind.True)
                || Unix(sub, "cancel_at") is not null;
        }
        else
        {
            user.Plan = UserPlan.Free;
            user.StripeSubscriptionId = null;
            user.PlanRenewsAt = null;
            user.PlanCancelAtPeriodEnd = false;
        }
    }

    /// <summary><c>current_period_end</c> sits on the subscription (old API versions) or on its first item (new ones).</summary>
    private static DateTime? PeriodEnd(JsonElement sub)
    {
        if (Unix(sub, "current_period_end") is { } direct) return direct;
        if (sub.TryGetProperty("items", out var items) && items.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            return Unix(data[0], "current_period_end");
        return null;
    }

    private static DateTime? Unix(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var s)
            ? DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime
            : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
