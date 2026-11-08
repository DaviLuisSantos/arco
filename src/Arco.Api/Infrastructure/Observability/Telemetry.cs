using System.Diagnostics;
using System.Diagnostics.Metrics;
using Arco.Api.Infrastructure.Email;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Arco.Api.Infrastructure.Observability;

/// <summary>Fontes de traces e métricas do app. Sem a variável de ambiente do Application Insights, nada é exportado.</summary>
public static class Telemetry
{
    public const string Name = "Arco.Api";
    public const string ConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    public static readonly ActivitySource Source = new(Name);
    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> CheckIns = Meter.CreateCounter<long>("arco.checkins.created");
    public static readonly Counter<long> PushSent = Meter.CreateCounter<long>("arco.push.sent");
    public static readonly Counter<long> PushFailed = Meter.CreateCounter<long>("arco.push.failed");
    public static readonly Counter<long> PlanLimitBlocked = Meter.CreateCounter<long>("arco.plan_limit.blocked");

    /// <summary>Liga o OpenTelemetry (Azure Monitor) só quando a connection string existe.</summary>
    public static void AddArcoTelemetry(this IServiceCollection services, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config[ConnectionStringKey])) return;

        // Estes logs citam o e-mail da pessoa (e o outro, o corpo com token): ficam só no log local.
        services.AddLogging(l => l
            .AddFilter<OpenTelemetryLoggerProvider>(typeof(LogEmailSender).FullName, LogLevel.None)
            .AddFilter<OpenTelemetryLoggerProvider>(OwnerBootstrap.LogCategory, LogLevel.None));

        services.AddOpenTelemetry()
            .UseAzureMonitor()
            .WithTracing(t => t
                .AddSource(Name)
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddProcessor(new PiiScrubbingProcessor()))
            .WithMetrics(m => m.AddMeter(Name));
    }
}

/// <summary>Remove das spans o que identifica pessoa: query string, IP, user agent e usuário.</summary>
public sealed class PiiScrubbingProcessor : BaseProcessor<Activity>
{
    private static readonly string[] Exact = ["url.query", "client.address", "http.client_ip", "user_agent.original", "http.user_agent", "db.statement", "db.query.text"];

    public override void OnEnd(Activity activity)
    {
        foreach (var key in Exact) activity.SetTag(key, null);
        foreach (var tag in activity.TagObjects.Select(t => t.Key).Where(k => k.StartsWith("enduser.", StringComparison.Ordinal)).ToList())
            activity.SetTag(tag, null);
    }
}
