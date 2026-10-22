namespace Arco.Api.Domain;

public readonly record struct PlayerProgress(int Level, int TotalXp, int XpIntoLevel, int XpForNextLevel);

public static class PlayerLevel
{
    /// <summary>XP needed to go from <paramref name="level"/> to level + 1: 100 + 50 × (L − 1).</summary>
    public static int CostToAdvance(int level) => 100 + 50 * (Math.Max(1, level) - 1);

    public static PlayerProgress FromTotalXp(int totalXp)
    {
        var remaining = Math.Max(0, totalXp);
        var level = 1;
        while (remaining >= CostToAdvance(level))
        {
            remaining -= CostToAdvance(level);
            level++;
        }
        return new PlayerProgress(level, totalXp, remaining, CostToAdvance(level));
    }
}
