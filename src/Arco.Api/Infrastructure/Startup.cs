using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Arco.Api.Features.Auth;
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
    public const string AuthScheme = "arco";
    public const string AuthCookieName = "arco_auth";
    public const string LoginRateLimitPolicy = "login";

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

        // Web Push: VAPID keys (generated into the data dir when not configured) and the reminder loop.
        services.AddSingleton(VapidKeys.Load(config, dataDir));
        services.AddHttpClient<IPushSender, WebPushSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<PushNotifier>();
        services.AddScoped<ReminderDispatcher>();
        if (config.GetValue("Push:SchedulerEnabled", true)) services.AddHostedService<ReminderService>();

        // JSON: camelCase (default for web) + enums as strings.
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        // Malformed JSON / bad route values throw BadHttpRequestException, mapped to 400 { code: "validation" }.
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

        // Auth
        services.AddSingleton(PasswordVerifier.FromConfiguration(config, env));
        services.AddAuthentication(AuthScheme).AddCookie(AuthScheme, o =>
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
                OnRedirectToLogin = ctx => ApiResults.WriteErrorAsync(ctx.HttpContext,
                    StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, "Não autenticado."),
                OnRedirectToAccessDenied = ctx => ApiResults.WriteErrorAsync(ctx.HttpContext,
                    StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Acesso negado."),
            };
        });
        services.AddAuthorization();

        // Login rate limit: N per minute per client IP (fixed window).
        var permitLimit = config.GetValue("Auth:LoginAttemptsPerMinute", 5);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (ctx, ct) => new ValueTask(ApiResults.WriteErrorAsync(ctx.HttpContext,
                StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited,
                "Muitas tentativas de login. Aguarde um minuto e tente novamente.", ct));
            o.AddPolicy(LoginRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        // Runs behind Caddy.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });
    }

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
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

        if (app.Services.GetRequiredService<PasswordVerifier>().UsingDevelopmentDefault)
        {
            app.Logger.LogWarning("Auth:PasswordHash is not configured; accepting the development password \"{Password}\".",
                PasswordVerifier.DevelopmentPassword);
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
        secured.MapHabits();
        secured.MapCycles();
        secured.MapGoals();
        secured.MapPlayer();
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
