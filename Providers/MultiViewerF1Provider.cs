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

    // Implementação da Interface: Nome da categoria fixa como F1 [4, 5]
    public string SeriesName => "F1";

    public MultiViewerF1Provider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _state = new RaceStateModel { Series = "F1" };
    }

    // Implementação da Interface: Inicia o loop de polling respeitando o CancellationToken [6, 7]
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[MV F1] Iniciando provedor GraphQL...");
        _pollTask = PollLoopAsync(cancellationToken);
        return Task.CompletedTask;
    }

    // Implementação da Interface: Para o provedor [7]
    public Task StopAsync()
    {
        Console.WriteLine("[MV F1] Provedor parado.");
        return Task.CompletedTask;
    }

    // Implementação da Interface: Retorna o estado atualizado para o Orchestrator [5, 8]
    public RaceStateModel GetCurrentState() => _state;

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await UpdateStateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MV F1 Error]: {ex.Message}");
            }
            // Polling de 1 segundo para manter o overlay fluido no Meld Studio [6, 9]
            await Task.Delay(1000, ct);
        }
    }

    private async Task UpdateStateAsync()
    {
        // Query utilizando o novo nó f1LiveTimingState (o antigo liveTimingState está depreciado) [2, 10]
        var query = new
        {
            query = @"
            query {
              f1LiveTimingState {
                TimingData
                WeatherData
                TrackStatus
                SessionInfo
              }
            }"
        };

        var response = await _httpClient.PostAsJsonAsync(_graphqlUrl, query);
        if (!response.IsSuccessStatusCode) return;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        // Navegação segura no payload do MultiViewer [3, 11]
        if (!json.TryGetProperty("data", out var dataNode) || 
            !dataNode.TryGetProperty("f1LiveTimingState", out var stateNode) ||
            stateNode.ValueKind == JsonValueKind.Null) return;

        // 1. Atualizar Metadados da Sessão, Clima e Status da Pista [12-14]
        if (stateNode.TryGetProperty("SessionInfo", out var session))
            _state.Circuit = session.GetProperty("Meeting").GetProperty("Circuit").GetProperty("ShortName").GetString() ?? _state.Circuit;

        if (stateNode.TryGetProperty("TrackStatus", out var status))
            _state.TrackStatus = status.GetProperty("Message").GetString() ?? "GREEN";

        if (stateNode.TryGetProperty("WeatherData", out var weather))
        {
            _state.AirTemp = weather.GetProperty("airtemp").GetString() ?? "--";
            _state.TrackTemp = weather.GetProperty("tracktemp").GetString() ?? "--";
            _state.WindSpeed = weather.GetProperty("windspeed").GetString() ?? "--";
        }

        // 2. Parser Robusto para TimingData (Suporta tanto Array quanto Objeto/Dicionário) [3, 15]
        if (stateNode.TryGetProperty("TimingData", out var timingNode))
        {
            var newStandings = new List<DriverStandingModel>();

            if (timingNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in timingNode.EnumerateArray())
                    ProcessDriverEntry(entry, newStandings);
            }
            else if (timingNode.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in timingNode.EnumerateObject())
                    ProcessDriverEntry(prop.Value, newStandings);
            }

            // Ordenação para garantir que a torre de classificação no overlay fique estável [8, 16]
            _state.Standings = newStandings.OrderBy(d => d.Position).ToList();
        }
    }

    private void ProcessDriverEntry(JsonElement entry, List<DriverStandingModel> list)
    {
        try
        {
            // O MultiViewer costuma retornar números como strings no JSON de timing [3, 17]
            int position = 0;
            if (entry.TryGetProperty("Position", out var p)) int.TryParse(p.GetString(), out position);
            
            if (position == 0) return;

            list.Add(new DriverStandingModel
            {
                Position = position,
                Tla = entry.TryGetProperty("Abbreviation", out var tla) ? tla.GetString() ?? "" : "",
                FullName = entry.TryGetProperty("Abbreviation", out var name) ? name.GetString() ?? "" : "",
                Gap = entry.TryGetProperty("GapToLeader", out var gap) ? gap.GetString() ?? "" : "",
                BestLap = entry.TryGetProperty("BestLapTime", out var best) ? best.GetProperty("Value").GetString() ?? "" : ""
            });
        }
        catch { /* Ignora falhas em linhas malformadas para evitar crash do provedor [3] */ }
    }
}