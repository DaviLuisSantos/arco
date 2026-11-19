using Arco.Api.Features.Achievements;
using Arco.Api.Features.CheckIns;

namespace Arco.Api.Features.Quantities;

public static class QuantityCodes
{
    public const string QuantityRequired = "quantity_required";
    public const string NotQuantitative = "not_quantitative";
    public const string InvalidQuantity = "invalid_quantity";
    public const string DateOutOfRange = "date_out_of_range";
}

/// <param name="CheckIn">The day's check-in after the change (null when there is none).</param>
/// <param name="XpDelta">XP gained (positive) or lost (negative) by the change.</param>
public sealed record QuantityOutcome(CheckIn? CheckIn, int XpDelta);

/// <summary>
/// Writes a quantitative habit's day total and derives the check-in (and its XP) from it, in one transaction.
/// The caller validates the habit (quantitative, not archived) and the date window.
/// </summary>
public sealed class QuantityService(AppDbContext db, AppClock clock)
{
    /// <summary>
    /// Stores the absolute total of the day (0 removes it) and applies the resulting status: Done/Minimum create or
    /// update the check-in and its XP; below the minimum, a Done/Minimum check-in is removed with its XP (a manual
    /// Missed/Skipped stays). Saves once; <paramref name="habit"/> must have its levels loaded.
    /// </summary>
    public async Task<QuantityOutcome> ApplyAsync(Habit habit, DateOnly date, int totalTenths, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var row = await db.HabitDayQuantities.FirstOrDefaultAsync(q => q.HabitId == habit.Id && q.Date == date, ct);
        if (totalTenths <= 0)
        {
            if (row is not null) db.HabitDayQuantities.Remove(row);
        }
        else if (row is null)
        {
            db.HabitDayQuantities.Add(new HabitDayQuantity { HabitId = habit.Id, Date = date, TotalTenths = totalTenths, UpdatedAt = now });
        }
        else
        {
            row.TotalTenths = totalTenths;
            row.UpdatedAt = now;
        }
        return await DeriveAndSaveAsync(habit, date, Math.Max(0, totalTenths), now, ct);
    }

    /// <summary>
    /// Recomputes today's status after the level changed (new goal/minimum). Returns the XP change; does nothing for
    /// a regular habit or a day without a total. Saves on its own.
    /// </summary>
    public async Task<int> RecalculateAsync(Habit habit, DateOnly today, CancellationToken ct)
    {
        if (habit.Unit is null) return 0;
        var row = await db.HabitDayQuantities.AsNoTracking().FirstOrDefaultAsync(q => q.HabitId == habit.Id && q.Date == today, ct);
        if (row is null) return 0;
        return (await DeriveAndSaveAsync(habit, today, row.TotalTenths, clock.UtcNow, ct)).XpDelta;
    }

    private async Task<QuantityOutcome> DeriveAndSaveAsync(Habit habit, DateOnly date, int totalTenths, DateTime now, CancellationToken ct)
    {
        CheckIn? checkIn;
        var xpDelta = 0;
        var revoke = false;
        var level = habit.CurrentLevel;
        if (level?.TargetTenths is not { } target || level.MinimumTenths is not { } minimum)
        {
            // No numbers on the current level: nothing to derive, only the total is stored.
            checkIn = await db.CheckIns.FirstOrDefaultAsync(c => c.HabitId == habit.Id && c.Date == date, ct);
        }
        else if (QuantityRules.StatusFor(totalTenths, target, minimum) is { } status)
        {
            (checkIn, xpDelta) = await CheckInEndpoints.ApplyStatusAsync(db, habit, date, status, now, false, null, ct);
        }
        else
        {
            checkIn = await db.CheckIns.FirstOrDefaultAsync(c => c.HabitId == habit.Id && c.Date == date, ct);
            if (checkIn is { Status: CheckInStatus.Done or CheckInStatus.Minimum })
            {
                var events = await db.XpEvents.Where(e => e.CheckInId == checkIn.Id).ToListAsync(ct);
                xpDelta = -events.Sum(e => e.Amount);
                db.XpEvents.RemoveRange(events);
                db.CheckIns.Remove(checkIn);
                checkIn = null;
                revoke = true;
            }
        }

        await db.SaveChangesAsync(ct);
        if (revoke) await AchievementService.RevokeUnmetAsync(db, clock, ct);
        return new QuantityOutcome(checkIn, xpDelta);
    }
}
