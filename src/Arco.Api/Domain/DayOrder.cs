namespace Arco.Api.Domain;

/// <summary>Pure rules for the order of the day: periods, manual order and "after…" chains.</summary>
public static class DayOrder
{
    /// <summary>Morning 05:00–11:59, afternoon 12:00–17:59, evening 18:00–04:59.</summary>
    public static TimeOfDay CurrentPeriod(TimeOnly localTime) => localTime.Hour switch
    {
        >= 5 and < 12 => TimeOfDay.Morning,
        >= 12 and < 18 => TimeOfDay.Afternoon,
        _ => TimeOfDay.Evening,
    };

    public sealed record Item(int Id, string Name, TimeOfDay TimeOfDay, int SortOrder, int? AnchorHabitId);

    /// <summary>Position of a period in the day: Morning, Afternoon, Evening, then Anytime.</summary>
    public static int PeriodRank(TimeOfDay period) => period switch
    {
        TimeOfDay.Morning => 0,
        TimeOfDay.Afternoon => 1,
        TimeOfDay.Evening => 2,
        _ => 3,
    };

    /// <summary>
    /// Ids in the order of the day: by period, then <see cref="Item.SortOrder"/>, then name; then every habit chained to a
    /// habit of the same period goes right after its anchor (A → B → C stay in sequence). An anchor in another period, or
    /// outside <paramref name="items"/>, only matters as a label.
    /// </summary>
    public static IReadOnlyList<int> Order(IEnumerable<Item> items)
    {
        var sorted = items
            .OrderBy(i => PeriodRank(i.TimeOfDay)).ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(i => i.Id)
            .ToList();
        var byId = sorted.ToDictionary(i => i.Id);

        bool IsChained(Item i) =>
            i.AnchorHabitId is { } a && a != i.Id && byId.TryGetValue(a, out var anchor) && anchor.TimeOfDay == i.TimeOfDay;

        var children = sorted.Where(IsChained).GroupBy(i => i.AnchorHabitId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<int>(sorted.Count);
        var seen = new HashSet<int>();

        void Emit(Item item)
        {
            if (!seen.Add(item.Id)) return;
            result.Add(item.Id);
            if (children.TryGetValue(item.Id, out var next))
                foreach (var child in next) Emit(child);
        }

        foreach (var item in sorted.Where(i => !IsChained(i))) Emit(item);
        // Defensive: members of a cycle (not creatable through the API) still show up.
        foreach (var item in sorted) Emit(item);
        return result;
    }

    /// <summary>
    /// True when anchoring <paramref name="habitId"/> to <paramref name="anchorId"/> would close a loop (including
    /// anchoring a habit to itself). <paramref name="anchors"/> maps each habit id to its current anchor habit id.
    /// </summary>
    public static bool CreatesCycle(int habitId, int anchorId, IReadOnlyDictionary<int, int?> anchors)
    {
        var visited = new HashSet<int>();
        int? current = anchorId;
        while (current is { } id)
        {
            if (id == habitId) return true;
            if (!visited.Add(id)) return false; // an existing loop that does not involve this habit
            current = anchors.TryGetValue(id, out var next) ? next : null;
        }
        return false;
    }
}
