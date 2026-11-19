using System.Net;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class WeeklyReviewMoodTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo; tests move to Sunday 2030-03-10 (the week is reviewable).
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<int> HabitAsync(HttpClient c, string name, bool weekly = false)
    {
        var res = await c.PostAsJsonAsync("/api/habits", new
        {
            name, frequencyType = weekly ? "WeeklyTarget" : "Daily", timesPerWeek = weekly ? 3 : (int?)null,
            levelDescription = "10 min", minimumDescription = "2 min",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task CheckInAsync(HttpClient c, int habitId, string date, string status, string? note = null)
    {
        var res = await c.PutAsJsonAsync("/api/checkins", new { habitId, date, status, note });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private static async Task MoodAsync(HttpClient c, string date, int value)
    {
        var res = await c.PutAsJsonAsync("/api/moods", new { date, value });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Notes_are_listed_by_date_then_habit_and_mood_is_summarized()
    {
        var c = await _factory.LoggedInClientAsync();
        var run = await HabitAsync(c, "Correr");
        var read = await HabitAsync(c, "Ler", weekly: true); // weekly target: no due day, out of the consistency

        _factory.Time!.AdvanceDays(6); // Sunday 2030-03-10

        await CheckInAsync(c, run, "2030-03-06", "Minimum", "Cansado");
        await CheckInAsync(c, read, "2030-03-04", "Done", "Capítulo 3");
        await CheckInAsync(c, run, "2030-03-04", "Done", "Foi bem");
        await CheckInAsync(c, run, "2030-03-05", "Missed");
        await CheckInAsync(c, run, "2030-03-08", "Done"); // no note: not listed

        await MoodAsync(c, "2030-03-04", 5);
        await MoodAsync(c, "2030-03-05", 4);
        await MoodAsync(c, "2030-03-06", 1);
        await MoodAsync(c, "2030-03-07", 3);

        var summary = await c.GetJsonAsync("/api/reviews/weekly/current");

        var notes = summary["notes"]!.AsArray();
        Assert.Equal(3, notes.Count);
        Assert.Equal(["2030-03-04", "2030-03-04", "2030-03-06"], notes.Select(n => (string)n!["date"]!).ToArray());
        Assert.Equal(["Correr", "Ler", "Correr"], notes.Select(n => (string)n!["habitName"]!).ToArray());
        Assert.Equal("Foi bem", (string?)notes[0]!["note"]);
        Assert.Equal("Minimum", (string?)notes[2]!["status"]);
        Assert.Equal(run, (int)notes[0]!["habitId"]!);

        var mood = summary["mood"]!;
        Assert.Equal(4, (int)mood["days"]!);
        Assert.Equal(3.3, (double)mood["average"]!, 1); // 13 / 4 = 3.25
        Assert.Equal(2, (int)mood["goodDays"]!);
        Assert.Equal(1, (int)mood["badDays"]!);
        Assert.Equal(0.5, (double)mood["goodConsistency"]!, 3); // Done on the 4th, Missed on the 5th
        Assert.Equal(1.0, (double)mood["badConsistency"]!, 3); // Minimum on the 6th
    }

    [Fact]
    public async Task Week_without_mood_or_notes_is_empty()
    {
        var c = await _factory.LoggedInClientAsync();
        var run = await HabitAsync(c, "Correr");
        _factory.Time!.AdvanceDays(6);
        await CheckInAsync(c, run, "2030-03-05", "Done");

        var summary = await c.GetJsonAsync("/api/reviews/weekly/current");

        Assert.Empty(summary["notes"]!.AsArray());
        var mood = summary["mood"]!;
        Assert.Equal(0, (int)mood["days"]!);
        Assert.Null(mood["average"]);
        Assert.Equal(0, (int)mood["goodDays"]!);
        Assert.Equal(0, (int)mood["badDays"]!);
        Assert.Null(mood["goodConsistency"]);
        Assert.Null(mood["badConsistency"]);
    }

    [Fact]
    public async Task Group_without_expected_check_ins_has_null_consistency()
    {
        var c = await _factory.LoggedInClientAsync();
        await HabitAsync(c, "Ler", weekly: true);
        _factory.Time!.AdvanceDays(6);
        await MoodAsync(c, "2030-03-05", 5);

        var mood = (await c.GetJsonAsync("/api/reviews/weekly/current"))["mood"]!;

        Assert.Equal(1, (int)mood["goodDays"]!);
        Assert.Null(mood["goodConsistency"]);
        Assert.Null(mood["badConsistency"]);
    }

    [Fact]
    public async Task Notes_and_mood_belong_to_the_owner_only()
    {
        var a = await _factory.LoggedInClientAsync();
        var b = await _factory.LoggedInClientAsync();
        var habit = await HabitAsync(a, "Correr");
        _factory.Time!.AdvanceDays(6);
        await CheckInAsync(a, habit, "2030-03-05", "Done", "Segredo");
        await MoodAsync(a, "2030-03-05", 4);

        var other = await b.GetJsonAsync("/api/reviews/weekly/current");

        Assert.Empty(other["notes"]!.AsArray());
        Assert.Equal(0, (int)other["mood"]!["days"]!);
    }
}
