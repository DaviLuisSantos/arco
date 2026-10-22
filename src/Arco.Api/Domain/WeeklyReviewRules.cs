namespace Arco.Api.Domain;

public static class WeeklyReviewRules
{
    public const int MaxAnswerLength = 2000;

    /// <summary>
    /// The week open for review: on Sunday it is the current week (it ends today);
    /// Monday to Saturday it is the previous week.
    /// </summary>
    public static DateOnly ReviewableWeekStart(DateOnly today) =>
        today.DayOfWeek == DayOfWeek.Sunday
            ? HabitSchedule.WeekStart(today)
            : HabitSchedule.WeekStart(today).AddDays(-7);
}
