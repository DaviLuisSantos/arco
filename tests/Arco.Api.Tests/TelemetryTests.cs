using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using Arco.Api.Infrastructure.Observability;

namespace Arco.Api.Tests;

// Os contadores são estáticos: em paralelo, o listener somaria os check-ins de outros testes.
[CollectionDefinition(nameof(TelemetryCollection), DisableParallelization = true)]
public sealed class TelemetryCollection;

[Collection(nameof(TelemetryCollection))]
public sealed class TelemetryTests : IDisposable
{
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Check_in_counter_increments_only_on_creation()
    {
        long total = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Meter.Name == Telemetry.Name && inst.Name == "arco.checkins.created") l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref total, value));
        listener.Start();

        var client = await _factory.LoggedInClientAsync();
        var res = await client.PostAsJsonAsync("/api/habits",
            new { name = "Estudar", frequencyType = "Daily", levelDescription = "1 h", minimumDescription = "10 min" });
        var habitId = (int)(await res.JsonAsync())["id"]!;

        var body = new { habitId, date = "2030-03-04", status = "Done" };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/checkins", body)).StatusCode);
        Assert.Equal(1, Interlocked.Read(ref total));

        await client.PutAsJsonAsync("/api/checkins", body); // update, not creation
        Assert.Equal(1, Interlocked.Read(ref total));
    }
}
