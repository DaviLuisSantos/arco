using System.Net;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class CheckInNoteTests : IDisposable
{
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateHabitAsync(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task<string?> PutAsync(HttpClient c, int id, string status, object? note, bool withNote = true)
    {
        object body = withNote
            ? new { habitId = id, date = "2030-03-04", status, note }
            : new { habitId = id, date = "2030-03-04", status };
        var res = await c.PutAsJsonAsync("/api/checkins", body);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await c.GetJsonAsync("/api/checkins?from=2030-03-04&to=2030-03-04");
        return (string?)list.AsArray()[0]!["note"];
    }

    [Fact]
    public async Task Absent_or_null_note_keeps_and_empty_removes()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c);

        Assert.Equal("cansado", await PutAsync(c, id, "Done", "cansado"));
        Assert.Equal("cansado", await PutAsync(c, id, "Minimum", null, withNote: false));
        Assert.Equal("cansado", await PutAsync(c, id, "Done", null));
        Assert.Null(await PutAsync(c, id, "Done", "   "));
    }

    [Theory]
    [InlineData("Done")]
    [InlineData("Minimum")]
    [InlineData("Missed")]
    public async Task Note_is_accepted_in_any_status(string status)
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c);
        Assert.Equal("contexto", await PutAsync(c, id, status, "contexto"));
    }
}
