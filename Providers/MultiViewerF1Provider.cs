using System.Net.Http.Json;
using System.Text.Json;
using MyRaceBackend.Models;

namespace MyRaceBackend.Providers;

public class MultiViewerF1Provider : IRaceDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly RaceStateModel _state;
    private readonly string _graphqlUrl = "http://localhost:10101/api/graphql";
    private Task? _pollTask;

    public string SeriesName => "F1";

    public MultiViewerF1Provider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _state = new RaceStateModel { Series = "F1" };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pollTask = PollLoopAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;

    public RaceStateModel GetCurrentState() => _state;

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await UpdateStateAsync(); }
            catch (Exception ex) { Console.WriteLine($"[MV F1 Error]: {ex.Message}"); }
            await Task.Delay(1000, ct);
        }
    }

    private async Task UpdateStateAsync()
    {
        var query = new { query = @"query { f1LiveTimingState { TimingData WeatherData TrackStatus SessionInfo } }" };
        var response = await _httpClient.PostAsJsonAsync(_graphqlUrl, query);
        if (!response.IsSuccessStatusCode) return;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!json.TryGetProperty("data", out var d) || !d.TryGetProperty("f1LiveTimingState", out var data)) return;

        // Atualiza Clima e Status
        if (data.TryGetProperty("TrackStatus", out var ts)) 
            _state.TrackStatus = ts.GetProperty("Message").GetString() ?? "GREEN";

        // Parser Robusto para TimingData (Array ou Object)
        if (data.TryGetProperty("TimingData", out var timingNode))
        {
            var newStandings = new List<DriverStandingModel>();
            if (timingNode.ValueKind == JsonValueKind.Array)
                foreach (var entry in timingNode.EnumerateArray()) ProcessDriverEntry(entry, newStandings);
            else if (timingNode.ValueKind == JsonValueKind.Object)
                foreach (var prop in timingNode.EnumerateObject()) ProcessDriverEntry(prop.Value, newStandings);

            _state.Standings = newStandings.OrderBy(d => d.Position).ToList();
        }
    }

    private void ProcessDriverEntry(JsonElement entry, List<DriverStandingModel> list)
    {
        try {
            int.TryParse(entry.GetProperty("Position").GetString(), out int pos);
            if (pos == 0) return;
            list.Add(new DriverStandingModel {
                Position = pos,
                Tla = entry.GetProperty("Abbreviation").GetString() ?? "",
                FullName = entry.GetProperty("Abbreviation").GetString() ?? "",
                Gap = entry.TryGetProperty("GapToLeader", out var g) ? g.GetString() ?? "" : ""
            });
        } catch { }
    }
}