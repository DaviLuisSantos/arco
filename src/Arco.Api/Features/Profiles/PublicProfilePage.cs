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
                "<title>Arco</title><body style=\"font-family:sans-serif;background:#0b0f14;color:#eef1f5;" +
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

        sb.Append("<header><span class=\"brand\">").Append(LogoSvg).Append("Arco</span></header>\n");
        sb.Append("<section class=\"hero\">\n<h1>").Append(H(p.DisplayName)).Append("</h1>\n");
        sb.Append("<p class=\"level\"><strong>").Append(H(t.Level)).Append(' ').Append(p.Level).Append("</strong> · ")
            .Append(p.TotalXp.ToString("N0", CultureInfo.GetCultureInfo(data.Locale))).Append(' ').Append(H(t.Xp))
            .Append("</p>\n</section>\n");

        if (p.Cycle is { } cycle)
        {
            sb.Append("<section class=\"card\">\n<h2>").Append(H(t.Cycle)).Append(": ").Append(H(cycle.Name)).Append("</h2>\n");
            sb.Append("<p class=\"muted\">").Append(H(t.Week)).Append(' ').Append(cycle.Week).Append('/')
                .Append(cycle.TotalWeeks).Append("</p>\n");
            AppendArch(sb, cycle, t);
            sb.Append("</section>\n");
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

    private const string LogoSvg =
        "<svg viewBox=\"0 0 32 32\" fill=\"none\" aria-hidden=\"true\"><defs><linearGradient id=\"lg\" x1=\"0\" x2=\"1\">" +
        "<stop offset=\"0\" stop-color=\"#6366f1\"/><stop offset=\"1\" stop-color=\"#14b8a6\"/></linearGradient></defs>" +
        "<path d=\"M4 26a12 12 0 0 1 24 0\" stroke=\"url(#lg)\" stroke-width=\"4\" stroke-linecap=\"round\"/></svg>";

    /// <summary>The cycle arch: one stone per week on a half ring, opacity following the focus rate.</summary>
    private static void AppendArch(StringBuilder sb, ProfileEndpoints.PublicCycleDto cycle, Texts t)
    {
        const double cx = 120, cy = 124, outer = 108, inner = 66, gap = 2.5;
        var n = cycle.Stones.Count;
        var sweep = 180.0 / n;
        string P(double r, double deg) => string.Create(CultureInfo.InvariantCulture,
            $"{cx + r * Math.Cos(deg * Math.PI / 180):0.##} {cy + r * Math.Sin(deg * Math.PI / 180):0.##}");
        sb.Append("<svg class=\"arch\" viewBox=\"0 0 240 132\" role=\"img\" aria-label=\"")
            .Append(H($"{t.Cycle}: {t.Week} {cycle.Week}/{cycle.TotalWeeks}")).Append("\">");
        for (var i = 0; i < n; i++)
        {
            var a0 = 180 + i * sweep + gap / 2;
            var a1 = 180 + (i + 1) * sweep - gap / 2;
            var opacity = (0.15 + 0.85 * cycle.Stones[i].Rate).ToString("0.00", CultureInfo.InvariantCulture);
            sb.Append("<path d=\"M").Append(P(outer, a0)).Append(" A").Append(outer).Append(' ').Append(outer)
                .Append(" 0 0 1 ").Append(P(outer, a1)).Append(" L").Append(P(inner, a1)).Append(" A").Append(inner)
                .Append(' ').Append(inner).Append(" 0 0 0 ").Append(P(inner, a0)).Append("Z\" fill=\"var(--accent)\" opacity=\"")
                .Append(opacity).Append("\"><title>").Append(H($"{t.Week} {cycle.Stones[i].Week}")).Append("</title></path>");
        }
        sb.Append("</svg>\n");
    }

    private static string Css(string accent)
    {
        var c = SKColor.Parse(accent);
        var luminance = (0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue) / 255;
        var accentFg = luminance > 0.62 ? "#14181f" : "#fff";
        return ":root{--accent:" + accent + ";--accent-fg:" + accentFg + ";--bg:#fafaf7;--fg:#14181f;--muted:#5b6573;" +
               "--card:#fff;--line:#e5e5df}" +
               "@media(prefers-color-scheme:dark){:root{--bg:#0b0f14;--fg:#eef1f5;--muted:#98a2b3;--card:#121821;--line:#1f2733}}" +
               "*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);" +
               "font:16px/1.6 system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;-webkit-font-smoothing:antialiased}" +
               "main{max-width:640px;margin:0 auto;padding:20px 20px 56px}" +
               ".brand{display:inline-flex;align-items:center;gap:10px;font-weight:700;font-size:1.2rem;letter-spacing:-.02em}" +
               ".brand svg{width:28px;height:28px}" +
               ".hero{padding:32px 0 8px}h1{margin:0;font-size:2.1rem;line-height:1.15;letter-spacing:-.02em;overflow-wrap:anywhere}" +
               ".level{margin:10px 0 0;color:var(--muted)}.level strong{color:var(--accent);font-size:1.25rem}" +
               ".card{background:var(--card);border:1px solid var(--line);border-radius:18px;padding:22px;margin-top:16px}" +
               "h2{margin:0 0 2px;font-size:1.05rem;overflow-wrap:anywhere}.muted{color:var(--muted);margin:0}" +
               ".arch{display:block;width:100%;max-width:300px;margin:16px auto 0}" +
               ".habits{list-style:none;margin:12px 0 0;padding:0}" +
               ".habits li{display:flex;justify-content:space-between;gap:12px;padding:10px 0;border-top:1px solid var(--line)}" +
               ".habits li:first-child{border-top:0}.habits li span:first-child{overflow-wrap:anywhere;min-width:0}" +
               ".habits .muted{text-align:right;font-size:.9rem}" +
               ".cta{text-align:center;margin-top:32px}.cta a{display:inline-block;background:var(--accent);" +
               "color:var(--accent-fg);text-decoration:none;font-weight:600;padding:13px 24px;border-radius:999px}" +
               ".cta a:focus-visible{outline:2px solid var(--accent);outline-offset:3px}";
    }

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
        canvas.Clear(SKColor.Parse("#0b0f14"));

        using var text = new SKPaint { IsAntialias = true };

        // Left column: brand, name, level, cycle.
        text.Color = accent;
        DrawText(canvas, "Arco", 64, 96, Bold.Value, 34, text);

        text.Color = SKColor.Parse("#eef1f5");
        var nameSize = FitSize(p.DisplayName, Bold.Value, 64, 460);
        DrawText(canvas, p.DisplayName, 64, 300, Bold.Value, nameSize, text, 460);

        text.Color = accent;
        DrawText(canvas, $"{t.Level} {p.Level}", 64, 372, Bold.Value, 44, text);

        text.Color = SKColor.Parse("#98a2b3");
        DrawText(canvas, $"{p.TotalXp.ToString("N0", CultureInfo.GetCultureInfo(data.Locale))} {t.Xp}", 64, 424,
            Regular.Value, 30, text);

        if (p.Cycle is { } cycle)
        {
            DrawText(canvas, cycle.Name, 64, 508, Regular.Value, 28, text, 480);
            DrawText(canvas, $"{t.Week} {cycle.Week}/{cycle.TotalWeeks}", 64, 550, Regular.Value, 28, text, 480);
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

        text.Color = SKColor.Parse("#eef1f5");
        DrawText(canvas, p.Level.ToString(CultureInfo.InvariantCulture), cx, cy - 20, Bold.Value, 110, text, align: SKTextAlign.Center);
        text.Color = SKColor.Parse("#98a2b3");
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
