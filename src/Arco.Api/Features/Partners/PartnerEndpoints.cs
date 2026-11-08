using System.Security.Cryptography;
using Arco.Api.Features.Reviews;

namespace Arco.Api.Features.Partners;

/// <summary>Accountability partner: the owner invites someone by link, who then sees the owner's weekly summary.</summary>
public static class PartnerEndpoints
{
    public const int InviteDays = 7;
    private const int MaxPendingInvites = 10;

    public sealed record InviteDto(int Id, string Link, DateTime ExpiresAt);
    public sealed record AcceptRequest(string? Token);
    public sealed record OwnerNameDto(string DisplayName);
    public sealed record AcceptResult(int Id, OwnerNameDto Owner);
    public sealed record PartnerLinkDto(int Id, string DisplayName, DateTime Since);
    public sealed record PendingInviteDto(int Id, DateTime CreatedAt, DateTime ExpiresAt);
    public sealed record PartnersDto(
        IReadOnlyList<PartnerLinkDto> SharingWith,
        IReadOnlyList<PartnerLinkDto> SharedWithMe,
        IReadOnlyList<PendingInviteDto> PendingInvites);

    public sealed record SummaryOwnerDto(string DisplayName, int Level);
    public sealed record SummaryHabitDto(string Name, int Level, int Done, int Minimum, int Expected, double Rate, int Streak);
    public sealed record SummaryWeekDto(DateOnly Start, DateOnly End, IReadOnlyList<SummaryHabitDto> Habits, CheckInTotalsDto Totals);
    public sealed record PartnerSummaryDto(SummaryOwnerDto Owner, IReadOnlyList<SummaryWeekDto> Weeks);

    public static RouteGroupBuilder MapPartners(this RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/partners").WithTags("Parceiros");
        group.MapPost("/invites", CreateInvite).WithSummary("Cria um convite de parceiro (7 dias)")
            .Produces<InviteDto>(StatusCodes.Status201Created);
        group.MapPost("/accept", Accept).WithSummary("Aceita um convite de parceiro")
            .Produces<AcceptResult>();
        group.MapGet("", List).WithSummary("Lista parceiros e convites pendentes")
            .Produces<PartnersDto>();
        group.MapGet("/{id:int}/summary", Summary).WithSummary("Resumo semanal de quem compartilha com você")
            .Produces<PartnerSummaryDto>();
        group.MapDelete("/{id:int}", Remove).WithSummary("Remove a parceria (qualquer um dos lados)")
            .Produces(StatusCodes.Status204NoContent);
        return secured;
    }

    internal static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static async Task<IResult> CreateInvite(AppDbContext db, CurrentUser me, TimeProvider time,
        IConfiguration config, HttpContext http, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var pending = await db.Partnerships.CountAsync(
            p => p.OwnerUserId == me.Id && p.PartnerUserId == null && p.ExpiresAt > now, ct);
        if (pending >= MaxPendingInvites)
            return ApiResults.Validation($"Você já tem {MaxPendingInvites} convites pendentes. Remova algum antes de criar outro.");

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var partnership = new Partnership
        {
            OwnerUserId = me.Id,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = now.AddDays(InviteDays),
        };
        db.Partnerships.Add(partnership);
        await db.SaveChangesAsync(ct);

        var baseUrl = (config["App:BaseUrl"] ?? $"{http.Request.Scheme}://{http.Request.Host}").TrimEnd('/');
        return Results.Json(new InviteDto(partnership.Id, $"{baseUrl}/invite/{token}", partnership.ExpiresAt),
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Accept(AcceptRequest? request, AppDbContext db, CurrentUser me,
        TimeProvider time, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Token)) return ApiResults.Validation("Informe o convite.");
        var hash = HashToken(request.Token.Trim());
        var now = time.GetUtcNow().UtcDateTime;

        var partnership = await db.Partnerships.FirstOrDefaultAsync(p => p.TokenHash == hash, ct);
        if (partnership is null || partnership.AcceptedAt is not null || partnership.ExpiresAt <= now)
            return ApiResults.NotFound("Convite inválido ou expirado.");
        if (partnership.OwnerUserId == me.Id)
            return ApiResults.Validation("Você não pode aceitar o próprio convite.");
        if (await db.Partnerships.AnyAsync(p => p.OwnerUserId == partnership.OwnerUserId && p.PartnerUserId == me.Id, ct))
            return ApiResults.Conflict("Você já é parceiro desta pessoa.");

        partnership.PartnerUserId = me.Id;
        partnership.AcceptedAt = now;
        await db.SaveChangesAsync(ct);

        var ownerName = await db.Users.AsNoTracking().Where(u => u.Id == partnership.OwnerUserId)
            .Select(u => u.DisplayName).FirstAsync(ct);
        return Results.Ok(new AcceptResult(partnership.Id, new OwnerNameDto(ownerName)));
    }

    private static async Task<PartnersDto> List(AppDbContext db, CurrentUser me, TimeProvider time, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var sharingWith = await (from p in db.Partnerships.AsNoTracking()
                                 join u in db.Users.AsNoTracking() on p.PartnerUserId equals u.Id
                                 where p.OwnerUserId == me.Id && p.AcceptedAt != null
                                 orderby p.AcceptedAt
                                 select new PartnerLinkDto(p.Id, u.DisplayName, p.AcceptedAt!.Value)).ToListAsync(ct);
        var sharedWithMe = await (from p in db.Partnerships.AsNoTracking()
                                  join u in db.Users.AsNoTracking() on p.OwnerUserId equals u.Id
                                  where p.PartnerUserId == me.Id && p.AcceptedAt != null
                                  orderby p.AcceptedAt
                                  select new PartnerLinkDto(p.Id, u.DisplayName, p.AcceptedAt!.Value)).ToListAsync(ct);
        var pending = await db.Partnerships.AsNoTracking()
            .Where(p => p.OwnerUserId == me.Id && p.PartnerUserId == null && p.AcceptedAt == null && p.ExpiresAt > now)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PendingInviteDto(p.Id, p.CreatedAt, p.ExpiresAt))
            .ToListAsync(ct);
        return new PartnersDto(sharingWith, sharedWithMe, pending);
    }

    private static async Task<IResult> Summary(int id, AppDbContext db, CurrentUser me,
        IServiceScopeFactory scopes, CancellationToken ct)
    {
        // Only the partner may read it; anybody else (including the owner) gets 404.
        var ownerId = await db.Partnerships.AsNoTracking()
            .Where(p => p.Id == id && p.PartnerUserId == me.Id && p.AcceptedAt != null)
            .Select(p => (int?)p.OwnerUserId).FirstOrDefaultAsync(ct);
        if (ownerId is null) return ApiResults.NotFound();

        // The owner's data lives behind the per-user query filter: read it in a scope acting as the owner.
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().Impersonate(ownerId.Value);
        var ownerDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<AppClock>();

        var today = await clock.GetTodayAsync(ct);
        var timezone = await clock.GetTimezoneAsync(ct);
        var name = await ownerDb.Users.AsNoTracking().Where(u => u.Id == ownerId.Value)
            .Select(u => u.DisplayName).FirstAsync(ct);
        var level = PlayerLevel.FromTotalXp(await ownerDb.XpEvents.SumAsync(e => e.Amount, ct)).Level;

        var thisStart = HabitSchedule.WeekStart(today);
        var lastStart = thisStart.AddDays(-7);
        var ranges = new[] { (thisStart, today), (lastStart, HabitSchedule.WeekEnd(lastStart)) };

        // Streaks need each habit's whole history.
        var history = (await ownerDb.CheckIns.AsNoTracking().ToListAsync(ct)).ToLookup(c => c.HabitId);
        var habitsById = (await ownerDb.Habits.AsNoTracking().ToListAsync(ct)).ToDictionary(h => h.Id);

        var weeks = new List<SummaryWeekDto>();
        foreach (var (start, end) in ranges)
        {
            var stats = await ReviewStats.ForRangeAsync(ownerDb, start, end, today, timezone, ct);
            var habits = stats.Select(s => new SummaryHabitDto(s.Name, s.Level, s.Done, s.Minimum, s.Expected, s.Rate,
                habitsById.TryGetValue(s.HabitId, out var h)
                    ? Queries.Streak(h, history[s.HabitId].ToList(), today, timezone).Current
                    : 0)).ToList();
            weeks.Add(new SummaryWeekDto(start, end, habits, ReviewStats.Totals(stats)));
        }

        return Results.Ok(new PartnerSummaryDto(new SummaryOwnerDto(name, level), weeks));
    }

    private static async Task<IResult> Remove(int id, AppDbContext db, CurrentUser me, CancellationToken ct)
    {
        var partnership = await db.Partnerships
            .FirstOrDefaultAsync(p => p.Id == id && (p.OwnerUserId == me.Id || p.PartnerUserId == me.Id), ct);
        if (partnership is null) return ApiResults.NotFound();
        db.Partnerships.Remove(partnership);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
