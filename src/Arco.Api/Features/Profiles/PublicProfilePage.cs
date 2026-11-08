using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using SkiaSharp;

namespace Arco.Api.Features.Profiles;

/// <summary>The server-rendered <c>/u/{slug}</c> page (with Open Graph tags) and its 1200×630 PNG.</summary>
internal static class PublicProfilePage
{
    private const int ImageWidth = 1200;
    private const int ImageHeight = 630;

    private sealed record Texts(
        string Level, string Xp, string Cycle, string Week, string Habits, string Streak, string Cta, string NotFound,
        string Description);

    private static readonly Texts Pt = new(
        "Nível", "XP", "Ciclo", "Semana", "Hábitos", "dias seguidos", "Criar meu Arco", "Perfil não encontrado",
        "{0} está no nível {1} no Arco.");

    private static readonly Texts En = new(
        "Level", "XP", "Cycle", "Week", "Habits", "day streak", "Create my Arco", "Profile not found",
        "{0} is at level {1} on Arco.");

    private static Texts TextsFor(string locale) => locale == "en" ? En : Pt;

    // --- HTML ---

    public static async Task<IResult> RenderPage(
        string slug, HttpContext http, AppDbContext db, CurrentUser current, AppClock clock, IConfiguration config,
        CancellationToken ct)
    {
        var data = await ProfileEndpoints.LoadAsync(slug, db, current, clock, ct);
        if (data is null)
        {
            return Results.Content(
                "<!doctype html><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\">" +
                "<title>Arco</title><body style=\"font-family:sans-serif;background:#0b0d12;color:#e7e9ee;" +
                "display:grid;place-items:center;min-height:100vh\"><p>Perfil não encontrado · Profile not found</p>",
                "text/html; charset=utf-8", Encoding.UTF8, StatusCodes.Status404NotFound);
        }

        var baseUrl = BaseUrl(config, http);
        var p = data.Profile;
        var t = TextsFor(data.Locale);
        var accent = ProfileEndpoints.Themes[p.Theme];
        var title = $"{p.DisplayName} · Arco";
        var description = string.Format(CultureInfo.InvariantCulture, t.Description, p.DisplayName, p.Level)
                          + (p.Cycle is { } c ? $" {t.Cycle}: {c.Name} ({t.Week} {c.Week}/{c.TotalWeeks})." : "");
        var url = $"{baseUrl}/u/{WebUtility.UrlEncode(slug.ToLowerInvariant())}";
        var image = $"{url}/og.png";

        var sb = new StringBuilder(4096);
        sb.Append("<!doctype html>\n<html lang=\"").Append(H(data.Locale)).Append("\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n");
        sb.Append("<title>").Append(H(title)).Append("</title>\n");
        sb.Append("<meta name=\"description\" content=\"").Append(H(description)).Append("\">\n");
        sb.Append("<link rel=\"canonical\" href=\"").Append(H(url)).Append("\">\n");
        sb.Append("<link rel=\"icon\" href=\"/favicon.svg\" type=\"image/svg+xml\">\n");
        sb.Append("<meta property=\"og:type\" content=\"profile\">\n");
        sb.Append("<meta property=\"og:site_name\" content=\"Arco\">\n");
        sb.Append("<meta property=\"og:title\" content=\"").Append(H(title)).Append("\">\n");
        sb.Append("<meta property=\"og:description\" content=\"").Append(H(description)).Append("\">\n");
        sb.Append("<meta property=\"og:url\" content=\"").Append(H(url)).Append("\">\n");
        sb.Append("<meta property=\"og:image\" content=\"").Append(H(image)).Append("\">\n");
        sb.Append("<meta property=\"og:image:width\" content=\"").Append(ImageWidth).Append("\">\n");
        sb.Append("<meta property=\"og:image:height\" content=\"").Append(ImageHeight).Append("\">\n");
        sb.Append("<meta name=\"twitter:card\" content=\"summary_large_image\">\n");
        sb.Append("<meta name=\"twitter:title\" content=\"").Append(H(title)).Append("\">\n");
        sb.Append("<meta name=\"twitter:description\" content=\"").Append(H(description)).Append("\">\n");
        sb.Append("<meta name=\"twitter:image\" content=\"").Append(H(image)).Append("\">\n");
        sb.Append("<style>").Append(Css(accent)).Append("</style>\n</head>\n<body>\n<main>\n");

        sb.Append("<header><span class=\"brand\">Arco</span></header>\n");
        sb.Append("<section class=\"hero\">\n<h1>").Append(H(p.DisplayName)).Append("</h1>\n");
        sb.Append("<p class=\"level\"><strong>").Append(H(t.Level)).Append(' ').Append(p.Level).Append("</strong> · ")
            .Append(p.TotalXp.ToString("N0", CultureInfo.GetCultureInfo(data.Locale))).Append(' ').Append(H(t.Xp))
            .Append("</p>\n</section>\n");

        if (p.Cycle is { } cycle)
        {
            sb.Append("<section class=\"card\">\n<h2>").Append(H(t.Cycle)).Append(": ").Append(H(cycle.Name)).Append("</h2>\n");
            sb.Append("<p class=\"muted\">").Append(H(t.Week)).Append(' ').Append(cycle.Week).Append('/')
                .Append(cycle.TotalWeeks).Append("</p>\n<div class=\"stones\">");
            foreach (var stone in cycle.Stones)
            {
                var opacity = (0.15 + 0.85 * stone.Rate).ToString("0.00", CultureInfo.InvariantCulture);
                sb.Append("<span class=\"stone\" style=\"opacity:").Append(opacity).Append("\" title=\"")
                    .Append(H($"{t.Week} {stone.Week}")).Append("\"></span>");
            }
            sb.Append("</div>\n</section>\n");
        }

        if (p.Habits is { Count: > 0 } habits)
        {
            sb.Append("<section class=\"card\">\n<h2>").Append(H(t.Habits)).Append("</h2>\n<ul class=\"habits\">\n");
            foreach (var habit in habits)
            {
                sb.Append("<li><span>").Append(H(habit.Name)).Append("</span><span class=\"muted\">")
                    .Append(H(t.Level)).Append(' ').Append(habit.Level);
                if (habit.Streak > 0) sb.Append(" · ").Append(habit.Streak).Append(' ').Append(H(t.Streak));
                sb.Append("</span></li>\n");
            }
            sb.Append("</ul>\n</section>\n");
        }

        sb.Append("<p class=\"cta\"><a href=\"").Append(H(baseUrl)).Append("/\">").Append(H(t.Cta)).Append("</a></p>\n");
        sb.Append("</main>\n</body>\n</html>\n");

        http.Response.Headers.CacheControl = "public, max-age=300";
        return Results.Content(sb.ToString(), "text/html; charset=utf-8", Encoding.UTF8);
    }

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string Css(string accent) =>
        ":root{--accent:" + accent + ";--bg:#0b0d12;--fg:#e7e9ee;--muted:#8b93a7;--card:#141822;--line:#232938}" +
        "*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);" +
        "font:16px/1.5 system-ui,-apple-system,'Segoe UI',Roboto,sans-serif}" +
        "main{max-width:640px;margin:0 auto;padding:24px 16px 48px}" +
        ".brand{font-weight:700;letter-spacing:.04em;color:var(--accent)}" +
        ".hero{padding:32px 0 16px}h1{margin:0;font-size:2rem;line-height:1.2;overflow-wrap:anywhere}" +
        ".level{margin:8px 0 0;color:var(--muted)}.level strong{color:var(--accent);font-size:1.25rem}" +
        ".card{background:var(--card);border:1px solid var(--line);border-radius:16px;padding:20px;margin-top:16px}" +
        "h2{margin:0 0 4px;font-size:1.05rem;overflow-wrap:anywhere}.muted{color:var(--muted);margin:0}" +
        ".stones{display:flex;gap:6px;margin-top:14px}" +
        ".stone{flex:1;height:28px;border-radius:8px;background:var(--accent)}" +
        ".habits{list-style:none;margin:12px 0 0;padding:0}" +
        ".habits li{display:flex;justify-content:space-between;gap:12px;padding:10px 0;border-top:1px solid var(--line)}" +
        ".habits li:first-child{border-top:0}.habits li span:first-child{overflow-wrap:anywhere}" +
        ".habits .muted{white-space:nowrap;font-size:.9rem}" +
        ".cta{text-align:center;margin-top:32px}.cta a{display:inline-block;background:var(--accent);color:#fff;" +
        "text-decoration:none;font-weight:600;padding:12px 24px;border-radius:999px}";

    private static string BaseUrl(IConfiguration config, HttpContext http) =>
        (config["App:BaseUrl"] ?? $"{http.Request.Scheme}://{http.Request.Host}").TrimEnd('/');

    // --- PNG ---

    public static async Task<IResult> RenderImage(
        string slug, HttpContext http, AppDbContext db, CurrentUser current, AppClock clock, CancellationToken ct)
    {
        var data = await ProfileEndpoints.LoadAsync(slug, db, current, clock, ct);
        if (data is null) return Results.NotFound();

        var png = DrawImage(data);
        http.Response.Headers.CacheControl = "public, max-age=3600";
        return Results.File(png, "image/png");
    }

    private static readonly Lazy<SKTypeface> Regular = new(() => LoadFont("Inter-Regular.otf"));
    private static readonly Lazy<SKTypeface> Bold = new(() => LoadFont("Inter-Bold.otf"));

    private static SKTypeface LoadFont(string file)
    {
        var assembly = typeof(PublicProfilePage).Assembly;
        var name = assembly.GetManifestResourceNames().First(n => n.EndsWith(file, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return SKTypeface.FromStream(stream) ?? SKTypeface.Default;
    }

    private static byte[] DrawImage(ProfileEndpoints.PublicProfileData data)
    {
        var p = data.Profile;
        var t = TextsFor(data.Locale);
        var accent = SKColor.Parse(ProfileEndpoints.Themes[p.Theme]);

        using var surface = SKSurface.Create(new SKImageInfo(ImageWidth, ImageHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColor.Parse("#0b0d12"));

        using var text = new SKPaint { IsAntialias = true };

        // Left column: brand, name, level, cycle.
        text.Color = accent;
        DrawText(canvas, "Arco", 64, 96, Bold.Value, 34, text);

        text.Color = SKColor.Parse("#e7e9ee");
        var nameSize = FitSize(p.DisplayName, Bold.Value, 64, 460);
        DrawText(canvas, p.DisplayName, 64, 300, Bold.Value, nameSize, text, 460);

        text.Color = accent;
        DrawText(canvas, $"{t.Level} {p.Level}", 64, 372, Bold.Value, 44, text);

        text.Color = SKColor.Parse("#8b93a7");
        DrawText(canvas, $"{p.TotalXp.ToString("N0", CultureInfo.GetCultureInfo(data.Locale))} {t.Xp}", 64, 424,
            Regular.Value, 30, text);

        if (p.Cycle is { } cycle)
        {
            DrawText(canvas, $"{cycle.Name} · {t.Week} {cycle.Week}/{cycle.TotalWeeks}", 64, 540, Regular.Value, 28, text, 480);
        }

        // Right: the cycle arch, one stone per week.
        const float cx = 880, cy = 470, outer = 280, inner = 190;
        var rates = p.Cycle?.Stones.Select(s => s.Rate).ToList() ?? Enumerable.Repeat(0.0, 6).ToList();
        var n = rates.Count;
        const float gap = 3f;
        var sweep = 180f / n;
        // Each stone is a thick arc stroke (butt caps) centered between the inner and outer radius.
        const float mid = (outer + inner) / 2;
        using var stone = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = outer - inner, StrokeCap = SKStrokeCap.Butt,
        };
        var midRect = new SKRect(cx - mid, cy - mid, cx + mid, cy + mid);
        for (var i = 0; i < n; i++)
        {
            var start = 180f + i * sweep + gap / 2;
            var rate = (float)Math.Clamp(rates[i], 0, 1);
            stone.Color = accent.WithAlpha((byte)(45 + 210 * rate));
            canvas.DrawArc(midRect, start, sweep - gap, false, stone);
        }

        text.Color = SKColor.Parse("#e7e9ee");
        DrawText(canvas, p.Level.ToString(CultureInfo.InvariantCulture), cx, cy - 20, Bold.Value, 110, text, align: SKTextAlign.Center);
        text.Color = SKColor.Parse("#8b93a7");
        DrawText(canvas, t.Level, cx, cy + 20, Regular.Value, 30, text, align: SKTextAlign.Center);

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static float FitSize(string value, SKTypeface face, float max, float width)
    {
        using var font = new SKFont(face, max);
        var measured = font.MeasureText(value);
        return measured <= width ? max : Math.Max(28f, max * width / measured);
    }

    /// <summary>Draws text; when it still does not fit in <paramref name="maxWidth"/>, cuts it with an ellipsis.</summary>
    private static void DrawText(
        SKCanvas canvas, string value, float x, float y, SKTypeface face, float size, SKPaint paint,
        float? maxWidth = null, SKTextAlign align = SKTextAlign.Left)
    {
        using var font = new SKFont(face, size) { Edging = SKFontEdging.Antialias };
        if (maxWidth is { } limit && font.MeasureText(value) > limit)
        {
            while (value.Length > 1 && font.MeasureText(value + "…") > limit) value = value[..^1];
            value = value.TrimEnd() + "…";
        }
        canvas.DrawText(value, x, y, align, font, paint);
    }
}
