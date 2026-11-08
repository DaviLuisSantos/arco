using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Arco.Api.Domain;
using Arco.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Arco.Api.Tests;

public sealed class BillingApiTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_secret";

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>The same app, with the Stripe webhook secret set (checkout/portal stay disabled: no key or price).</summary>
    private WebApplicationFactory<Program> WithWebhook() =>
        _factory.WithWebHostBuilder(b => b.UseSetting("Stripe:WebhookSecret", WebhookSecret));

    private async Task SetPlanAsync(int userId, UserPlan plan)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.Plan = plan;
        await db.SaveChangesAsync();
    }

    private async Task<User> LoadUserAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    private static object HabitBody(string name) => new
    {
        name, frequencyType = "Daily", levelDescription = "15 min", minimumDescription = "5 min",
    };

    private static async Task<HttpResponseMessage> CreateHabitAsync(HttpClient client, string name) =>
        await client.PostAsJsonAsync("/api/habits", HabitBody(name));

    private static async Task AssertPlanLimitAsync(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("plan_limit", (string?)(await res.JsonAsync())["code"]);
    }

    private static async Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, string payload, string? secret = WebhookSecret)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = secret is null
            ? "t=1,v1=00"
            : $"t={ts},v1={Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{ts}.{payload}"))).ToLowerInvariant()}";
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("Stripe-Signature", signature);
        return await client.SendAsync(req);
    }

    private static string Event(string id, string type, string obj) =>
        "{\"id\":\"" + id + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"type\":\"" + type + "\",\"data\":{\"object\":" + obj + "}}";

    [Fact]
    public async Task Free_by_default_and_stripe_is_disabled_without_config()
    {
        var client = await _factory.LoggedInClientAsync();

        var billing = await client.GetJsonAsync("/api/billing");
        Assert.Equal("free", (string?)billing["plan"]);
        Assert.False((bool)billing["stripeEnabled"]!);
        Assert.Equal(5, (int)billing["limits"]!["habits"]!);
        Assert.Equal(1, (int)billing["limits"]!["apiKeys"]!);
        Assert.False((bool)billing["limits"]!["mcp"]!);
        Assert.False((bool)billing["limits"]!["profileTheme"]!);
        Assert.Equal(0, (int)billing["usage"]!["habits"]!);

        foreach (var path in new[] { "/api/billing/checkout", "/api/billing/portal" })
        {
            var res = await client.PostAsync(path, null);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            Assert.Equal("stripe_disabled", (string?)(await res.JsonAsync())["code"]);
        }

        var webhook = await PostWebhookAsync(_factory.CreateClient(), "{}");
        Assert.Equal(HttpStatusCode.NotFound, webhook.StatusCode);
    }

    [Fact]
    public async Task Billing_requires_a_session_but_the_webhook_does_not()
    {
        using var anon = WithWebhook().CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/billing")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostWebhookAsync(anon, "{}", secret: null)).StatusCode);
    }

    [Fact]
    public async Task Free_plan_caps_active_habits_and_pro_lifts_it()
    {
        var client = await _factory.LoggedInClientAsync();
        var ids = new List<int>();
        for (var i = 1; i <= 5; i++)
        {
            var res = await CreateHabitAsync(client, $"Hábito {i}");
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            ids.Add((int)(await res.JsonAsync())["id"]!);
        }

        await AssertPlanLimitAsync(await CreateHabitAsync(client, "Hábito 6"));
        Assert.Equal(5, (int)(await client.GetJsonAsync("/api/billing"))["usage"]!["habits"]!);

        // Archiving frees a slot; restoring past the limit is blocked.
        Assert.True((await client.PostAsync($"/api/habits/{ids[0]}/archive", null)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateHabitAsync(client, "Hábito 6")).StatusCode);
        await AssertPlanLimitAsync(await client.PostAsync($"/api/habits/{ids[0]}/restore", null));

        // Restoring a habit that is not archived changes nothing and is not blocked.
        Assert.True((await client.PostAsync($"/api/habits/{ids[1]}/restore", null)).IsSuccessStatusCode);

        // Editing is never limited.
        Assert.True((await client.PutAsJsonAsync($"/api/habits/{ids[1]}", HabitBody("Renomeado"))).IsSuccessStatusCode);

        await SetPlanAsync(await _factory.UserIdAsync(await EmailOfFirstAsync()), UserPlan.Pro);
        Assert.Equal(HttpStatusCode.Created, (await CreateHabitAsync(client, "Hábito 7")).StatusCode);
        Assert.True((await client.PostAsync($"/api/habits/{ids[0]}/restore", null)).IsSuccessStatusCode);
        var billing = await client.GetJsonAsync("/api/billing");
        Assert.Equal("pro", (string?)billing["plan"]);
        Assert.Null(billing["limits"]!["habits"]);
        Assert.True((bool)billing["limits"]!["mcp"]!);
    }

    private string? _email;

    private async Task<string> EmailOfFirstAsync()
    {
        if (_email is not null) return _email;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return _email = await db.Users.OrderBy(u => u.Id).Select(u => u.Email).FirstAsync();
    }

    [Fact]
    public async Task Free_plan_allows_one_api_key_and_pro_allows_more()
    {
        var client = await _factory.LoggedInClientAsync();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/api-keys", new { name = "A" })).StatusCode);
        await AssertPlanLimitAsync(await client.PostAsJsonAsync("/api/api-keys", new { name = "B" }));

        await SetPlanAsync(await _factory.UserIdAsync(await EmailOfFirstAsync()), UserPlan.Pro);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/api-keys", new { name = "B" })).StatusCode);
    }

    [Fact]
    public async Task Mcp_is_blocked_on_free_and_open_on_pro()
    {
        var client = await _factory.LoggedInClientAsync();
        var key = (string)(await (await client.PostAsJsonAsync("/api/api-keys", new { name = "MCP" })).JsonAsync())["key"]!;

        async Task<HttpResponseMessage> CallMcpAsync()
        {
            using var http = _factory.CreateClient(new() { HandleCookies = false });
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/mcp")
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"ping"}""", Encoding.UTF8, "application/json"),
            };
            req.Headers.Authorization = new("Bearer", key);
            req.Headers.Accept.ParseAdd("application/json");
            req.Headers.Accept.ParseAdd("text/event-stream");
            return await http.SendAsync(req);
        }

        await AssertPlanLimitAsync(await CallMcpAsync());

        await SetPlanAsync(await _factory.UserIdAsync(await EmailOfFirstAsync()), UserPlan.Pro);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await CallMcpAsync()).StatusCode);
    }

    [Fact]
    public async Task Webhook_rejects_a_bad_signature()
    {
        using var factory = WithWebhook();
        using var anon = factory.CreateClient();
        var payload = Event("evt_bad", "customer.subscription.deleted", """{"id":"sub_1","customer":"cus_1"}""");
        var res = await PostWebhookAsync(anon, payload, secret: "whsec_wrong");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Webhook_upgrades_updates_and_downgrades_without_deleting_anything()
    {
        using var factory = WithWebhook();
        using var anon = factory.CreateClient();
        var client = await _factory.LoggedInClientAsync();
        var userId = await _factory.UserIdAsync(await EmailOfFirstAsync());

        var done = Event("evt_1", "checkout.session.completed",
            $$"""{"id":"cs_1","object":"checkout.session","client_reference_id":"{{userId}}","customer":"cus_1","subscription":"sub_1"}""");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(anon, done)).StatusCode);
        var user = await LoadUserAsync(userId);
        Assert.Equal(UserPlan.Pro, user.Plan);
        Assert.Equal("cus_1", user.StripeCustomerId);
        Assert.Equal("sub_1", user.StripeSubscriptionId);

        // Renewal date and "cancel at period end", on the subscription (old layout).
        var updated = Event("evt_2", "customer.subscription.updated",
            """{"id":"sub_1","object":"subscription","customer":"cus_1","status":"active","cancel_at_period_end":true,"current_period_end":1900000000}""");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(anon, updated)).StatusCode);
        user = await LoadUserAsync(userId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1900000000).UtcDateTime, user.PlanRenewsAt);
        Assert.True(user.PlanCancelAtPeriodEnd);
        var billing = await client.GetJsonAsync("/api/billing");
        Assert.Equal("pro", (string?)billing["plan"]);
        Assert.True((bool)billing["cancelAtPeriodEnd"]!);
        Assert.NotNull(billing["renewsAt"]);

        // The customer portal on current API versions sets cancel_at and keeps cancel_at_period_end false.
        var portalCancel = Event("evt_cancel_at", "customer.subscription.updated",
            """{"id":"sub_1","customer":"cus_1","status":"active","cancel_at_period_end":false,"cancel_at":1900000000,"current_period_end":1900000000}""");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(anon, portalCancel)).StatusCode);
        Assert.True((await LoadUserAsync(userId)).PlanCancelAtPeriodEnd);

        var portalUndo = Event("evt_cancel_at_undo", "customer.subscription.updated",
            """{"id":"sub_1","customer":"cus_1","status":"active","cancel_at_period_end":false,"cancel_at":null,"current_period_end":1900000000}""");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(anon, portalUndo)).StatusCode);
        Assert.False((await LoadUserAsync(userId)).PlanCancelAtPeriodEnd);

        // Same, with the period end on the first item (new layout).
        var moved = Event("evt_3", "customer.subscription.updated",
            """{"id":"sub_1","customer":"cus_1","status":"active","cancel_at_period_end":false,"items":{"data":[{"current_period_end":1950000000}]}}""");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(anon, moved)).StatusCode);
        user = await LoadUserAsync(userId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1950000000).UtcDateTime, user.PlanRenewsAt);
        Assert.False(user.PlanCancelAtPeriodEnd);

        for (var i = 1; i <= 6; i++)
            Assert.Equal(HttpStatusCode.Created, (await CreateHabitAsync(client, $"Hábito {i}")).StatusCode);

        var deleted = Event("evt_4", "customer.subscription.deleted", """{"id":"sub_1","customer":"cus_1","status":"canceled"}""");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(anon, deleted)).StatusCode);
        user = await LoadUserAsync(userId);
        Assert.Equal(UserPlan.Free, user.Plan);
        Assert.Null(user.StripeSubscriptionId);
        Assert.Null(user.PlanRenewsAt);
        Assert.False(user.PlanCancelAtPeriodEnd);

        // Nothing was deleted; only creating is blocked.
        Assert.Equal(6, (await client.GetJsonAsync("/api/habits")).AsArray().Count);
        await AssertPlanLimitAsync(await CreateHabitAsync(client, "Hábito 7"));
    }

    [Fact]
    public async Task Webhook_is_idempotent_by_event_id()
    {
        using var factory = WithWebhook();
        using var anon = factory.CreateClient();
        await _factory.LoggedInClientAsync();
        var userId = await _factory.UserIdAsync(await EmailOfFirstAsync());

        var done = Event("evt_same", "checkout.session.completed",
            $$"""{"id":"cs_1","client_reference_id":"{{userId}}","customer":"cus_1","subscription":"sub_1"}""");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(anon, done)).StatusCode);

        // The user goes back to Free behind the webhook's back; redelivering the same event must not upgrade again.
        await SetPlanAsync(userId, UserPlan.Free);
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(anon, done)).StatusCode);
        Assert.Equal(UserPlan.Free, (await LoadUserAsync(userId)).Plan);

        // A different event id is processed.
        var again = Event("evt_other", "checkout.session.completed",
            $$"""{"id":"cs_2","client_reference_id":"{{userId}}","customer":"cus_1","subscription":"sub_1"}""");
        Assert.Equal(HttpStatusCode.OK, (await PostWebhookAsync(anon, again)).StatusCode);
        Assert.Equal(UserPlan.Pro, (await LoadUserAsync(userId)).Plan);
    }

    private const string BaseUrl = "https://arco.test";

    /// <summary>Records what the app sends to Stripe and answers with canned JSON; nothing touches the network.</summary>
    private sealed class FakeStripeHandler(HttpStatusCode status = HttpStatusCode.OK, string? body = null) : HttpMessageHandler
    {
        public sealed record Call(string Path, string? Authorization, System.Collections.Specialized.NameValueCollection Form);

        public List<Call> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var form = System.Web.HttpUtility.ParseQueryString(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            Calls.Add(new(request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString(), form));
            var json = body ?? (request.RequestUri.AbsolutePath.Contains("billing_portal")
                ? """{"id":"bps_1","object":"billing_portal.session","url":"https://billing.stripe.test/p/session_1"}"""
                : """{"id":"cs_1","object":"checkout.session","url":"https://checkout.stripe.test/c/pay/cs_1"}""");
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    /// <summary>The app with Stripe configured and its HTTP traffic routed to <paramref name="handler"/>.</summary>
    private WebApplicationFactory<Program> WithStripe(FakeStripeHandler handler) =>
        _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Stripe:SecretKey", "sk_test_fake");
            b.UseSetting("Stripe:PriceId", "price_pro");
            b.UseSetting("App:BaseUrl", BaseUrl + "/");
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<Arco.Api.Features.Billing.StripeClientFactory>();
                services.AddSingleton<Arco.Api.Features.Billing.StripeClientFactory>(key =>
                    new Stripe.StripeClient(key, httpClient: new Stripe.SystemNetHttpClient(new HttpClient(handler))));
            });
        });

    private async Task SetStripeStateAsync(int userId, string? customerId, string? subscriptionId = null, UserPlan plan = UserPlan.Free)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.StripeCustomerId = customerId;
        user.StripeSubscriptionId = subscriptionId;
        user.Plan = plan;
        await db.SaveChangesAsync();
    }

    private async Task<(HttpClient Client, int UserId, string Email)> StripeUserAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var email = await _factory.RegisterAsync(client);
        return (client, await _factory.UserIdAsync(email), email);
    }

    [Fact]
    public async Task Billing_reports_stripe_enabled_when_configured()
    {
        using var factory = WithStripe(new FakeStripeHandler());
        var (client, _, _) = await StripeUserAsync(factory);
        Assert.True((bool)(await client.GetJsonAsync("/api/billing"))["stripeEnabled"]!);
    }

    [Fact]
    public async Task Checkout_creates_a_subscription_session_for_the_user()
    {
        var handler = new FakeStripeHandler();
        using var factory = WithStripe(handler);
        var (client, userId, email) = await StripeUserAsync(factory);

        var res = await client.PostAsync("/api/billing/checkout", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("https://checkout.stripe.test/c/pay/cs_1", (string?)(await res.JsonAsync())["url"]);
        var call = Assert.Single(handler.Calls);
        Assert.Equal("/v1/checkout/sessions", call.Path);
        Assert.Equal("Bearer sk_test_fake", call.Authorization);
        Assert.Equal("subscription", call.Form["mode"]);
        Assert.Equal("price_pro", call.Form["line_items[0][price]"]);
        Assert.Equal("1", call.Form["line_items[0][quantity]"]);
        Assert.Equal(userId.ToString(), call.Form["client_reference_id"]);
        Assert.Equal(userId.ToString(), call.Form["subscription_data[metadata][userId]"]);
        Assert.Equal(email, call.Form["customer_email"]);
        Assert.Null(call.Form["customer"]);
        Assert.Equal($"{BaseUrl}/profile?checkout=success", call.Form["success_url"]);
        Assert.Equal($"{BaseUrl}/profile?checkout=cancel", call.Form["cancel_url"]);
    }

    [Fact]
    public async Task Checkout_reuses_the_existing_stripe_customer()
    {
        var handler = new FakeStripeHandler();
        using var factory = WithStripe(handler);
        var (client, userId, _) = await StripeUserAsync(factory);
        await SetStripeStateAsync(userId, "cus_123");

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/billing/checkout", null)).StatusCode);

        var call = Assert.Single(handler.Calls);
        Assert.Equal("cus_123", call.Form["customer"]);
        Assert.Null(call.Form["customer_email"]);
    }

    [Fact]
    public async Task Checkout_is_refused_for_someone_already_subscribed()
    {
        var handler = new FakeStripeHandler();
        using var factory = WithStripe(handler);
        var (client, userId, _) = await StripeUserAsync(factory);
        await SetStripeStateAsync(userId, "cus_123", "sub_123", UserPlan.Pro);

        var res = await client.PostAsync("/api/billing/checkout", null);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Checkout_and_portal_answer_502_when_stripe_fails()
    {
        var handler = new FakeStripeHandler(HttpStatusCode.BadRequest,
            """{"error":{"type":"invalid_request_error","message":"No such price"}}""");
        using var factory = WithStripe(handler);
        var (client, userId, _) = await StripeUserAsync(factory);
        await SetStripeStateAsync(userId, "cus_123");

        foreach (var path in new[] { "/api/billing/checkout", "/api/billing/portal" })
        {
            var res = await client.PostAsync(path, null);
            Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
            Assert.Equal("internal_error", (string?)(await res.JsonAsync())["code"]);
        }
    }

    [Fact]
    public async Task Portal_without_a_stripe_customer_is_a_clear_404()
    {
        var handler = new FakeStripeHandler();
        using var factory = WithStripe(handler);
        var (client, _, _) = await StripeUserAsync(factory);

        var res = await client.PostAsync("/api/billing/portal", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("not_found", (string?)(await res.JsonAsync())["code"]);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Portal_opens_the_billing_portal_of_the_customer()
    {
        var handler = new FakeStripeHandler();
        using var factory = WithStripe(handler);
        var (client, userId, _) = await StripeUserAsync(factory);
        await SetStripeStateAsync(userId, "cus_123", "sub_123", UserPlan.Pro);

        var res = await client.PostAsync("/api/billing/portal", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("https://billing.stripe.test/p/session_1", (string?)(await res.JsonAsync())["url"]);
        var call = Assert.Single(handler.Calls);
        Assert.Equal("/v1/billing_portal/sessions", call.Path);
        Assert.Equal("Bearer sk_test_fake", call.Authorization);
        Assert.Equal("cus_123", call.Form["customer"]);
        Assert.Equal($"{BaseUrl}/profile", call.Form["return_url"]);
    }

    [Fact]
    public async Task Checkout_and_portal_require_a_session()
    {
        using var factory = WithStripe(new FakeStripeHandler());
        using var anon = factory.CreateClient();
        foreach (var path in new[] { "/api/billing/checkout", "/api/billing/portal" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync(path, null)).StatusCode);
    }

    [Fact]
    public async Task Webhook_ignores_events_it_does_not_know()
    {
        using var factory = WithWebhook();
        using var anon = factory.CreateClient();
        var res = await PostWebhookAsync(anon, Event("evt_x", "invoice.paid", """{"id":"in_1"}"""));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
