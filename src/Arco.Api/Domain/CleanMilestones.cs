namespace Arco.Api.Domain;

/// <summary>Clean-day milestones of a negative habit: reaching one suggests the next level.</summary>
public static class CleanMilestones
{
    public static readonly int[] Days = [7, 14, 30, 60, 90, 180, 365];

    /// <summary>The clean days needed to leave <paramref name="currentLevel"/>; null past the last milestone.</summary>
    public static int? ForLevel(int currentLevel) =>
        currentLevel >= 1 && currentLevel <= Days.Length ? Days[currentLevel - 1] : null;

    public static bool ShouldSuggest(int cleanDays, int currentLevel) =>
        ForLevel(currentLevel) is { } milestone && cleanDays >= milestone;
}
