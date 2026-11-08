using System.Text.RegularExpressions;

namespace Arco.Api.Features.Profiles;

public static partial class ProfileEndpoints
{
    public sealed record UpdatePublicProfileRequest(bool? Enabled, string? Slug, bool? ShowHabits, string? Theme);

    public sealed record PublicProfileSettingsDto(bool Enabled, string? Slug, bool ShowHabits, string Theme);

    public sealed record PublicStoneDto(int Week, double Rate);

    public sealed record PublicCycleDto(string Name, int Week, int TotalWeeks, IReadOnlyList<PublicStoneDto> Stones);

    public sealed record PublicHabitDto(string Name, int Level, int Streak);

    public sealed record PublicProfileDto(
        string DisplayName,
        int Level,
        int TotalXp,
        string Theme,
        PublicCycleDto? Cycle,
        IReadOnlyList<PublicHabitDto>? Habits);

    /// <summary>Public profile data plus what the HTML page and the OG image need.</summary>
    internal sealed record PublicProfileData(PublicProfileDto Profile, string Locale);

    /// <param name="secured">Rotas autenticadas (<c>/api</c>).</param>
    /// <param name="anon">Rotas anônimas (<c>/api</c>), como <c>/api/public/profiles/{slug}</c>.</param>
    public static RouteGroupBuilder MapProfiles(this RouteGroupBuilder secured, RouteGroupBuilder anon)
    {
        secured.MapPut("/profile/public", UpdatePublic)
            .WithTags("Perfil público")
            .WithSummary("Configura o perfil público")
            .Produces<PublicProfileSettingsDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        anon.MapGet("/public/profiles/{slug}", GetPublic)
            .AllowAnonymous()
            .WithTags("Perfil público")
            .WithSummary("Lê um perfil público pelo slug")
            .Produces<PublicProfileDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return secured;
    }

    /// <summary>Páginas públicas fora de <c>/api</c>: <c>/u/{slug}</c> e <c>/u/{slug}/og.png</c>.</summary>
    public static WebApplication MapPublicProfilePage(this WebApplication app)
    {
        app.MapGet("/u/{slug}", PublicProfilePage.RenderPage).AllowAnonymous().ExcludeFromDescription();
        app.MapGet("/u/{slug}/og.png", PublicProfilePage.RenderImage).AllowAnonymous().ExcludeFromDescription();
        return app;
    }

    // --- Rules ---

    [GeneratedRegex("^[a-z0-9-]{3,30}$")]
    private static partial Regex SlugPattern();

    /// <summary>Slugs that would clash with routes or look official.</summary>
    internal static readonly HashSet<string> ReservedSlugs = new(StringComparer.Ordinal)
    {
        "admin", "api", "app", "login", "logout", "register", "signup", "signin", "invite", "welcome", "profile",
        "settings", "today", "habits", "cycles", "goals", "stats", "history", "player", "partners", "billing",
        "docs", "landing", "assets", "static", "public", "u", "me", "arco", "support", "help", "root", "www",
        "privacy", "terms", "about", "health", "forgot-password", "reset-password", "verify-email",
    };

    /// <summary>The theme the Free plan can use.</summary>
    internal const string DefaultTheme = "indigo";

    /// <summary>Theme name to accent color (RGB hex).</summary>
    internal static readonly IReadOnlyDictionary<string, string> Themes = new Dictionary<string, string>
    {
        ["indigo"] = "#6366f1",
        ["emerald"] = "#10b981",
        ["amber"] = "#f59e0b",
        ["rose"] = "#f43f5e",
        ["sky"] = "#0ea5e9",
    };

    // --- Handlers ---

    private static async Task<IResult> UpdatePublic(
        UpdatePublicProfileRequest? request, AppDbContext db, CurrentUser current, CancellationToken ct)
    {
        if (request is null) return ApiResults.Validation("Corpo da requisição ausente.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null) return ApiResults.Unauthorized();

        var slug = Text.Clean(request.Slug)?.ToLowerInvariant();
        if (slug is not null)
        {
            if (!SlugPattern().IsMatch(slug))
                return ApiResults.Validation("O endereço deve ter de 3 a 30 caracteres: letras minúsculas, números e hífen.");
            if (ReservedSlugs.Contains(slug))
                return ApiResults.Validation("Esse endereço é reservado. Escolha outro.");
        }

        var enabled = request.Enabled ?? user.PublicProfileEnabled;
        var effectiveSlug = request.Slug is null ? user.PublicSlug : slug;
        if (enabled && effectiveSlug is null)
            return ApiResults.Validation("Escolha um endereço para ativar o perfil público.");

        var theme = Text.Clean(request.Theme)?.ToLowerInvariant();
        if (theme is not null)
        {
            if (!Themes.ContainsKey(theme)) return ApiResults.Validation("Tema inválido.");
            if (theme != DefaultTheme && !PlanLimits.ProfileTheme(user.Plan))
                return ApiResults.Error(StatusCodes.Status403Forbidden, ErrorCodes.PlanLimit,
                    "Temas do perfil público fazem parte do plano Pro.");
        }

        if (effectiveSlug is not null && effectiveSlug != user.PublicSlug
            && await db.Users.AnyAsync(u => u.PublicSlug == effectiveSlug && u.Id != user.Id, ct))
            return ApiResults.Conflict("Esse endereço já está em uso.", ErrorCodes.SlugTaken);

        user.PublicProfileEnabled = enabled;
        user.PublicSlug = effectiveSlug;
        if (request.ShowHabits is { } showHabits) user.PublicShowHabits = showHabits;
        if (theme is not null) user.PublicTheme = theme;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race for the same slug (unique index).
            return ApiResults.Conflict("Esse endereço já está em uso.", ErrorCodes.SlugTaken);
        }

        return Results.Ok(new PublicProfileSettingsDto(
            user.PublicProfileEnabled, user.PublicSlug, user.PublicShowHabits, user.PublicTheme));
    }

    private static async Task<IResult> GetPublic(
        string slug, AppDbContext db, CurrentUser current, AppClock clock, CancellationToken ct)
    {
        var data = await LoadAsync(slug, db, current, clock, ct);
        return data is null ? ApiResults.NotFound("Perfil não encontrado.") : Results.Ok(data.Profile);
    }

    /// <summary>
    /// Reads an enabled profile by slug. Anonymous callers have no user, so the request acts as the owner
    /// (read-only) to reuse the per-user filters and "today" in the owner's timezone.
    /// </summary>
    internal static async Task<PublicProfileData?> LoadAsync(
        string slug, AppDbContext db, CurrentUser current, AppClock clock, CancellationToken ct)
    {
        var key = slug.ToLowerInvariant();
        var owner = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicSlug == key && u.PublicProfileEnabled, ct);
        if (owner is null) return null;

        current.Impersonate(owner.Id);
        clock.Invalidate();
        var today = await clock.GetTodayAsync(ct);
        var timezone = await clock.GetTimezoneAsync(ct);

        var totalXp = await db.XpEvents.SumAsync(e => e.Amount, ct);
        var level = PlayerLevel.FromTotalXp(totalXp).Level;

        PublicCycleDto? cycle = null;
        var active = await Queries.GetActiveCycleAsync(db, ct);
        if (active is not null)
        {
            var weeks = CycleMath.WeekCount(active.StartDate, active.EndDate);
            var sessions = await db.FocusSessions.AsNoTracking()
                .Where(s => s.Date >= active.StartDate && s.Date <= active.EndDate)
                .Select(s => new { s.Date, s.Minutes })
                .ToListAsync(ct);
            var minutesByWeek = sessions
                .GroupBy(s => CycleMath.WeekOfCycle(active.StartDate, s.Date))
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Minutes));
            var target = Math.Max(1, active.WeeklyFocusMinutes);
            var stones = Enumerable.Range(1, weeks)
                .Select(w => new PublicStoneDto(w,
                    Math.Round(Math.Min(1.0, minutesByWeek.GetValueOrDefault(w) / (double)target), 2)))
                .ToList();
            var week = Math.Clamp(CycleMath.WeekOfCycle(active.StartDate, today), 1, weeks);
            cycle = new PublicCycleDto(active.Name, week, weeks, stones);
        }

        List<PublicHabitDto>? habitDtos = null;
        if (owner.PublicShowHabits)
        {
            var habits = await db.Habits.AsNoTracking().Include(h => h.Levels)
                .Where(h => h.ArchivedAt == null)
                .ToListAsync(ct);
            var checkIns = (await db.CheckIns.AsNoTracking()
                    .Where(c => c.Date <= today && c.Habit!.ArchivedAt == null)
                    .ToListAsync(ct))
                .ToLookup(c => c.HabitId);
            habitDtos = habits
                .OrderBy(h => h.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(h => h.Id)
                .Select(h => new PublicHabitDto(h.Name, h.CurrentLevelNumber,
                    Queries.Streak(h, checkIns[h.Id].ToList(), today, timezone).Current))
                .ToList();
        }

        // A downgraded account keeps its stored theme, but only Pro shows it.
        var theme = PlanLimits.ProfileTheme(owner.Plan) && Themes.ContainsKey(owner.PublicTheme)
            ? owner.PublicTheme
            : DefaultTheme;

        return new PublicProfileData(
            new PublicProfileDto(owner.DisplayName, level, totalXp, theme, cycle, habitDtos),
            owner.Locale);
    }
}
