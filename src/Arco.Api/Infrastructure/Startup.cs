using Arco.Api.Features.Focus;
using Arco.Api.Features.Mcp;
using Arco.Api.Features.ApiKeys;
using Microsoft.AspNetCore.Authentication;
using Arco.Api.Features.Backup;
using Arco.Api.Features.Stats;
using Arco.Api.Features.Achievements;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Arco.Api.Features.Auth;
using Arco.Api.Features.Demo;
using Arco.Api.Infrastructure.Email;
using Microsoft.AspNetCore.Authentication.Google;
using Arco.Api.Features.CheckIns;
using Arco.Api.Features.Cycles;
using Arco.Api.Features.Goals;
using Arco.Api.Features.Habits;
using Arco.Api.Features.Health;
using Arco.Api.Features.Player;
using Arco.Api.Features.Push;
using Arco.Api.Features.Reviews;
using Arco.Api.Features.Settings;
using Arco.Api.Features.Today;
using Arco.Api.Infrastructure.Push;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.Sqlite;

namespace Arco.Api.Infrastructure;

public static class AppInfo
{
    public static string Version { get; } =
        (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
         ?? "0.0.0").Split('+')[0];
}

public static class Startup
{
    /// <summary>The app's cookie scheme.</summary>
    public const string AuthScheme = "arco";
    /// <summary>Picks the cookie or an API key per request.</summary>
    public const string DefaultScheme = "arco-or-api-key";
    public const string AuthCookieName = "arco_auth";
    public const string LoginRateLimitPolicy = "login";
    /// <summary>Register, forgot-password, resend-verification and demo.</summary>
    public const string AuthActionsRateLimitPolicy = "auth-actions";
    /// <summary>Temporary cookie that holds the Google identity between the provider callback and our own.</summary>
    public const string ExternalScheme = "arco-external";

    public static void AddArco(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;
        var env = builder.Environment;

        // Database
        var dbPath = ResolveDatabasePath(config, env.ContentRootPath, out var connectionString);
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));

        // Data protection keys live next to the database so auth cookies survive restarts.
        var dataDir = Path.GetDirectoryName(dbPath) ?? env.ContentRootPath;
        services.AddDataProtection()
            .SetApplicationName("Arco")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AppClock>();
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();
        services.AddArcoEmail(config);

        // Web Push: VAPID keys (generated into the data dir when not configured) and the reminder loop.
        services.AddSingleton(VapidKeys.Load(config, dataDir));
        services.AddHttpClient<IPushSender, WebPushSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<PushNotifier>();
        services.AddScoped<ReminderDispatcher>();
        if (config.GetValue("Push:SchedulerEnabled", true)) services.AddHostedService<ReminderService>();

        // Demo accounts are deleted after 24 hours.
        if (config.GetValue("Demo:CleanupEnabled", true)) services.AddHostedService<DemoCleanupService>();

        // Backups: one automatic copy per day next to the database, plus downloads on demand.
        services.AddSingleton(new DatabaseBackup(Path.Combine(dataDir, "backups")));
        if (config.GetValue("Backup:Enabled", true)) services.AddHostedService<DatabaseBackupService>();

        // MCP server at /api/mcp (Streamable HTTP, stateless), authenticated like the rest of /api. Its tools call the
        // REST API itself with the caller's key, so both always behave the same.
        services.AddHttpClient(ArcoApiClient.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<ArcoApiClient>();
        services.AddMcpServer(o =>
            {
                o.ServerInfo = new() { Name = "arco", Title = "Arco", Version = AppInfo.Version };
                o.ServerInstructions = McpInstructions.Text;
            })
            .WithHttpTransport(o => o.Stateless = true)
            .WithTools<ArcoTools>();

        // JSON: camelCase (default for web) + enums as strings.
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        // Malformed JSON / bad route values throw BadHttpRequestException, mapped to 400 { code: "validation" }.
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

        // Auth
        // Requests with "Authorization: Bearer …" use an API key; everything else, the app's cookie.
        var auth = services.AddAuthentication(DefaultScheme);
        auth.AddPolicyScheme(DefaultScheme, "Cookie ou chave de API", o =>
                o.ForwardDefaultSelector = ctx => ApiKeys.IsApiKeyRequest(ctx.Request) ? ApiKeys.Scheme : AuthScheme)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeys.Scheme, null)
            .AddCookie(AuthScheme, o =>
        {
            o.Cookie.Name = AuthCookieName;
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.Cookie.Path = "/";
            o.ExpireTimeSpan = TimeSpan.FromDays(30);
            o.SlidingExpiration = true;
            o.Events = new CookieAuthenticationEvents
            {
                // A password change or reset (new stamp) or a deleted account ends the session.
                OnValidatePrincipal = AuthSession.ValidateAsync,
                OnRedirectToLogin = ctx => ApiResults.WriteErrorAsync(ctx.HttpContext,
                    StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, "Não autenticado."),
                OnRedirectToAccessDenied = ctx => ApiResults.WriteErrorAsync(ctx.HttpContext,
                    StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Acesso negado."),
            };
        });
        if (GoogleAuth.IsConfigured(config))
        {
            auth.AddCookie(ExternalScheme, o =>
            {
                o.Cookie.Name = "arco_ext";
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });
            auth.AddGoogle(GoogleDefaults.AuthenticationScheme, o =>
            {
                o.ClientId = config["Auth:Google:ClientId"]!;
                o.ClientSecret = config["Auth:Google:ClientSecret"]!;
                o.SignInScheme = ExternalScheme;
                o.CallbackPath = Arco.Api.Features.Auth.GoogleAuth.ProviderCallbackPath;
                // Cancelled consent or bad state: back to the login screen instead of a 500.
                o.Events.OnRemoteFailure = ctx =>
                {
                    ctx.Response.Redirect("/login?error=google");
                    ctx.HandleResponse();
                    return Task.CompletedTask;
                };
                o.ClaimActions.MapJsonKey("email_verified", "email_verified");
            });
        }
        services.AddAuthorization();

        // Rate limits per client IP (fixed window, per minute): login attempts, and the other account actions.
        var loginLimit = config.GetValue("Auth:LoginAttemptsPerMinute", 5);
        var actionsLimit = config.GetValue("Auth:ActionsPerMinute", 10);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (ctx, ct) => new ValueTask(ApiResults.WriteErrorAsync(ctx.HttpContext,
                StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited,
                "Muitas tentativas. Aguarde um minuto e tente novamente.", ct));
            o.AddPolicy(LoginRateLimitPolicy, http => PerIpPerMinute(http, loginLimit));
            o.AddPolicy(AuthActionsRateLimitPolicy, http => PerIpPerMinute(http, actionsLimit));
        });

        // Runs behind Caddy.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });
    }

    private static RateLimitPartition<string> PerIpPerMinute(HttpContext http, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });

    /// <summary>
    /// Reads ConnectionStrings:Arco (default "Data Source=data/arco.db"), resolves a relative path
    /// against the content root and makes sure the directory exists.
    /// </summary>
    public static string ResolveDatabasePath(IConfiguration config, string contentRoot, out string connectionString)
    {
        var csb = new SqliteConnectionStringBuilder(config.GetConnectionString("Arco") ?? "Data Source=data/arco.db");
        var path = csb.DataSource;
        if (string.IsNullOrWhiteSpace(path) || path == ":memory:")
            throw new InvalidOperationException("ConnectionStrings:Arco must point to a SQLite file.");
        if (!Path.IsPathRooted(path)) path = Path.GetFullPath(Path.Combine(contentRoot, path));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        csb.DataSource = path;
        connectionString = csb.ToString();
        return path;
    }

    /// <summary>Applies pending migrations. Any failure propagates and stops the app.</summary>
    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        OwnerBootstrap.Run(app);

        // Achievements already earned before they existed unlock silently, for the pre-existing data (user 1).
        if (db.Users.AsNoTracking().Any(u => u.Id == OwnerBootstrap.OwnerId))
        {
            using var ownerScope = app.Services.CreateScope();
            ownerScope.ServiceProvider.GetRequiredService<CurrentUser>().Impersonate(OwnerBootstrap.OwnerId);
            AchievementService.UnlockAsync(
                    ownerScope.ServiceProvider.GetRequiredService<AppDbContext>(),
                    ownerScope.ServiceProvider.GetRequiredService<AppClock>(), CancellationToken.None)
                .GetAwaiter().GetResult();
        }
    }

    public static void UseArco(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler(handler => handler.Run(HandleExceptionAsync));

        app.Use(async (ctx, next) =>
        {
            var headers = ctx.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            await next();
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
    }

    public static void MapArco(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Anonymous: login, me, health.
        api.MapAuth();
        api.MapHealth();

        var secured = api.MapGroup("").RequireAuthorization();
        secured.MapToday();
        secured.MapCheckIns();
        secured.MapFocus();
        secured.MapHabits();
        secured.MapCycles();
        secured.MapCycleArch();
        secured.MapGoals();
        secured.MapPlayer();
        secured.MapAchievements();
        secured.MapStats();
        secured.MapBackup();
        secured.MapApiKeys();
        secured.MapMcp("/mcp");
        secured.MapReviews();
        secured.MapPush();
        secured.MapSettings();

        // Unknown /api routes are JSON 404s, never the SPA.
        api.MapFallback("{**path}", () => ApiResults.NotFound("Rota não encontrada."));

        // SPA: everything else that is not a file goes to index.html.
        app.MapFallbackToFile("index.html");
    }

    private static async Task HandleExceptionAsync(HttpContext ctx)
    {
        var error = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (error is BadHttpRequestException bad)
        {
            await ApiResults.WriteErrorAsync(ctx, bad.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? StatusCodes.Status413PayloadTooLarge
                    : StatusCodes.Status400BadRequest,
                ErrorCodes.Validation, "Requisição inválida: verifique o formato dos dados enviados.");
            return;
        }

        await ApiResults.WriteErrorAsync(ctx, StatusCodes.Status500InternalServerError,
            ErrorCodes.InternalError, "Erro interno");
    }
}
