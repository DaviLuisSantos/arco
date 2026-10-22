namespace Arco.Api.Domain;

/// <summary>Numbers the achievements are measured against (whole history, archived habits included).</summary>
public sealed record AchievementStats(
    int BestDayStreak,
    int BestWeekStreak,
    int CheckIns,
    int Minimums,
    int MaxHabitLevel,
    int PlayerLevel,
    int GoalsCompleted,
    int CyclesCompleted,
    int WeeklyReviews);

public sealed record AchievementDefinition(
    string Key,
    string Emoji,
    string Title,
    string Description,
    int Target,
    Func<AchievementStats, int> Metric);

/// <summary>
/// Achievements unlock by themselves from what the player already does; they give no XP and are never revoked.
/// </summary>
public static class AchievementCatalog
{
    public static readonly IReadOnlyList<AchievementDefinition> All =
    [
        // Constância
        new("streak-7", "🔥", "Uma semana firme", "Sequência de 7 dias em um hábito.", 7, s => s.BestDayStreak),
        new("streak-30", "🔥", "Um mês sem zerar", "Sequência de 30 dias em um hábito.", 30, s => s.BestDayStreak),
        new("streak-100", "💯", "Cem", "Sequência de 100 dias em um hábito.", 100, s => s.BestDayStreak),
        new("streak-weeks-4", "📅", "Quatro semanas", "Meta semanal batida 4 semanas seguidas.", 4, s => s.BestWeekStreak),
        new("streak-weeks-12", "📅", "Um trimestre", "Meta semanal batida 12 semanas seguidas.", 12, s => s.BestWeekStreak),
        // Volume
        new("checkins-1", "👣", "Primeiro passo", "Primeiro check-in com Feito ou Mínimo.", 1, s => s.CheckIns),
        new("checkins-100", "✅", "Cem check-ins", "100 check-ins com Feito ou Mínimo.", 100, s => s.CheckIns),
        new("checkins-500", "🏆", "Quinhentos", "500 check-ins com Feito ou Mínimo.", 500, s => s.CheckIns),
        // Não zerar
        new("minimum-10", "💪", "Não zerei", "10 dias salvos com o Mínimo.", 10, s => s.Minimums),
        // Progresso
        new("habit-level-2", "⬆️", "Subiu a dose", "Um hábito chegou ao nível 2.", 2, s => s.MaxHabitLevel),
        new("habit-level-5", "⭐", "Nível 5", "Um hábito chegou ao nível 5.", 5, s => s.MaxHabitLevel),
        new("player-5", "🎖️", "Jogador nível 5", "Chegou ao nível 5 de jogador.", 5, s => s.PlayerLevel),
        new("player-10", "👑", "Jogador nível 10", "Chegou ao nível 10 de jogador.", 10, s => s.PlayerLevel),
        // Direção
        new("goal-1", "🎯", "Objetivo cumprido", "Primeiro objetivo concluído.", 1, s => s.GoalsCompleted),
        new("cycle-1", "🏁", "Ciclo fechado", "Primeiro ciclo encerrado com revisão.", 1, s => s.CyclesCompleted),
        new("reviews-4", "📝", "Quatro revisões", "4 revisões semanais feitas.", 4, s => s.WeeklyReviews),
    ];

    /// <summary>Progress of every achievement, capped at its target (unlocked when progress == target).</summary>
    public static IEnumerable<(AchievementDefinition Definition, int Progress)> Evaluate(AchievementStats stats) =>
        All.Select(d => (d, Math.Clamp(d.Metric(stats), 0, d.Target)));
}
