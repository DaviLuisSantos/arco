using Arco.Api.Domain;

namespace Arco.Api.Tests;

public sealed class DayOrderTests
{
    private static DayOrder.Item Item(int id, string name, TimeOfDay period = TimeOfDay.Anytime, int sort = 0, int? anchor = null) =>
        new(id, name, period, sort, anchor);

    [Theory]
    [InlineData(4, 59, TimeOfDay.Evening)]
    [InlineData(5, 0, TimeOfDay.Morning)]
    [InlineData(11, 59, TimeOfDay.Morning)]
    [InlineData(12, 0, TimeOfDay.Afternoon)]
    [InlineData(17, 59, TimeOfDay.Afternoon)]
    [InlineData(18, 0, TimeOfDay.Evening)]
    [InlineData(23, 59, TimeOfDay.Evening)]
    [InlineData(0, 0, TimeOfDay.Evening)]
    public void CurrentPeriod_uses_the_hour_ranges(int hour, int minute, TimeOfDay expected) =>
        Assert.Equal(expected, DayOrder.CurrentPeriod(new TimeOnly(hour, minute)));

    [Fact]
    public void Order_groups_by_period_with_anytime_last()
    {
        var ids = DayOrder.Order([
            Item(1, "A", TimeOfDay.Anytime),
            Item(2, "B", TimeOfDay.Evening),
            Item(3, "C", TimeOfDay.Morning),
            Item(4, "D", TimeOfDay.Afternoon),
        ]);
        Assert.Equal([3, 4, 2, 1], ids);
    }

    [Fact]
    public void Order_uses_sort_order_then_name()
    {
        var ids = DayOrder.Order([
            Item(1, "Zeta", TimeOfDay.Morning, sort: 0),
            Item(2, "Beta", TimeOfDay.Morning, sort: 1),
            Item(3, "alfa", TimeOfDay.Morning, sort: 1),
            Item(4, "Omega", TimeOfDay.Morning, sort: 2),
        ]);
        Assert.Equal([1, 3, 2, 4], ids);
    }

    [Fact]
    public void Order_places_a_chain_right_after_its_anchor()
    {
        // C -> B -> A (C after B, B after A), all in the morning; D is a plain habit sorted between them.
        var ids = DayOrder.Order([
            Item(1, "A", TimeOfDay.Morning, sort: 0),
            Item(2, "D", TimeOfDay.Morning, sort: 1),
            Item(3, "B", TimeOfDay.Morning, sort: 2, anchor: 1),
            Item(4, "C", TimeOfDay.Morning, sort: 3, anchor: 3),
        ]);
        Assert.Equal([1, 3, 4, 2], ids);
    }

    [Fact]
    public void Order_keeps_siblings_of_one_anchor_in_manual_order()
    {
        var ids = DayOrder.Order([
            Item(1, "A", TimeOfDay.Morning, sort: 0),
            Item(2, "X", TimeOfDay.Morning, sort: 1),
            Item(3, "C", TimeOfDay.Morning, sort: 3, anchor: 1),
            Item(4, "B", TimeOfDay.Morning, sort: 2, anchor: 1),
        ]);
        Assert.Equal([1, 4, 3, 2], ids);
    }

    [Fact]
    public void Order_ignores_an_anchor_in_another_period_or_missing()
    {
        var ids = DayOrder.Order([
            Item(1, "A", TimeOfDay.Morning, sort: 0),
            Item(2, "B", TimeOfDay.Evening, sort: 0, anchor: 1), // anchor in another period: stays in its own group
            Item(3, "C", TimeOfDay.Morning, sort: 1, anchor: 99), // anchor not in the list (archived or deleted)
        ]);
        Assert.Equal([1, 3, 2], ids);
    }

    [Fact]
    public void Order_of_nothing_is_empty() => Assert.Empty(DayOrder.Order([]));

    [Fact]
    public void Order_survives_a_loop_in_the_data()
    {
        var ids = DayOrder.Order([
            Item(1, "A", TimeOfDay.Morning, anchor: 2),
            Item(2, "B", TimeOfDay.Morning, anchor: 1),
        ]);
        Assert.Equal([1, 2], ids.OrderBy(i => i));
    }

    [Fact]
    public void CreatesCycle_detects_self_direct_and_indirect_loops()
    {
        // 2 after 1, 3 after 2.
        var anchors = new Dictionary<int, int?> { [1] = null, [2] = 1, [3] = 2 };
        Assert.True(DayOrder.CreatesCycle(1, 1, anchors));
        Assert.True(DayOrder.CreatesCycle(1, 2, anchors)); // 1 after 2 after 1
        Assert.True(DayOrder.CreatesCycle(1, 3, anchors)); // 1 after 3 after 2 after 1
        Assert.False(DayOrder.CreatesCycle(3, 1, anchors));
        Assert.False(DayOrder.CreatesCycle(2, 4, anchors)); // an unknown anchor ends the walk
    }

    [Fact]
    public void CreatesCycle_ignores_an_existing_loop_that_does_not_include_the_habit()
    {
        var anchors = new Dictionary<int, int?> { [1] = 2, [2] = 1 };
        Assert.False(DayOrder.CreatesCycle(3, 1, anchors));
    }
}
