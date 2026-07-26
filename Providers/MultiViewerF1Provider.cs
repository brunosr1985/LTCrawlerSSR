using System.Text.Json;
using MyRaceBackend.Models;

namespace MyRaceBackend.Providers;

public class MultiViewerF1Provider : IRaceDataProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MultiViewerF1Provider> _logger;
    private readonly RaceStateModel _state;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public string SeriesName => "F1";

    public MultiViewerF1Provider(IHttpClientFactory httpClientFactory, ILogger<MultiViewerF1Provider> logger, RaceStateModel state)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _state = state;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isRunning) return Task.CompletedTask;

        _isRunning = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _ = Task.Run(async () =>
        {
            var client = _httpClientFactory.CreateClient();
            _logger.LogInformation("Starting MultiViewer F1 GraphQL polling loop...");

            // Query matching the official MultiViewer schema you provided
            var graphQLQuery = new
            {
                query = "{ f1LiveTimingState { TimingData WeatherData TrackStatus SessionInfo } }"
            };

            while (_isRunning && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var content = new StringContent(
                        JsonSerializer.Serialize(graphQLQuery), 
                        System.Text.Encoding.UTF8, 
                        "application/json"
                    );

                    var response = await client.PostAsync("http://localhost:10101/graphql", content, _cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync(_cts.Token);
                        ParseGraphQLData(jsonString);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query MultiViewer GraphQL endpoint.");
                    _state.Series = "F1";
                }

                await Task.Delay(1500, _cts.Token);
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _isRunning = false;
        _cts?.Cancel();
        _logger.LogInformation("Stopped MultiViewer F1 polling.");
        return Task.CompletedTask;
    }

    public RaceStateModel GetCurrentState() => _state;

    private void ParseGraphQLData(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _state.Series = "F1";

            if (root.TryGetProperty("data", out var dataProp) &&
                dataProp.TryGetProperty("f1LiveTimingState", out var f1StateProp))
            {
                // 1. Session Info (JSONObject fields)
                if (f1StateProp.TryGetProperty("SessionInfo", out var sessionInfoObj) && 
                    sessionInfoObj.ValueKind == JsonValueKind.Object)
                {
                    if (sessionInfoObj.TryGetProperty("Meeting", out var meetingObj) && 
                        meetingObj.TryGetProperty("Name", out var meetName))
                    {
                        _state.Circuit = meetName.GetString() ?? "F1 Grand Prix";
                    }
                }

                // 2. Track Status
                if (f1StateProp.TryGetProperty("TrackStatus", out var trackStatusObj) &&
                    trackStatusObj.ValueKind == JsonValueKind.Object)
                {
                    if (trackStatusObj.TryGetProperty("Message", out var statusMsg))
                    {
                        _state.TrackStatus = statusMsg.GetString() ?? "GREEN";
                    }
                }

                // 3. Weather Data
                if (f1StateProp.TryGetProperty("WeatherData", out var weatherObj) && 
                    weatherObj.ValueKind == JsonValueKind.Object)
                {
                    if (weatherObj.TryGetProperty("AirTemp", out var air)) _state.AirTemp = air.GetString() ?? "--";
                    if (weatherObj.TryGetProperty("TrackTemp", out var trk)) _state.TrackTemp = trk.GetString() ?? "--";
                    if (weatherObj.TryGetProperty("WindSpeed", out var wnd)) _state.WindSpeed = wnd.GetString() ?? "--";
                }

                // 4. Timing Data (Lines parsing safe for JSONObjects)
                if (f1StateProp.TryGetProperty("TimingData", out var timingDataObj) &&
                    timingDataObj.ValueKind == JsonValueKind.Object &&
                    timingDataObj.TryGetProperty("Lines", out var linesObj) &&
                    linesObj.ValueKind == JsonValueKind.Object)
                {
                    var standingsList = new List<DriverStandingModel>();

                    foreach (var prop in linesObj.EnumerateObject())
                    {
                        var driverData = prop.Value;
                        int pos = 99;
                        if (driverData.TryGetProperty("Position", out var posProp))
                        {
                            int.TryParse(posProp.GetString(), out pos);
                        }

                        string tla = driverData.TryGetProperty("Tla", out var tlaProp) ? tlaProp.GetString() ?? "" : "";
                        string fullName = driverData.TryGetProperty("FullName", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        string gap = driverData.TryGetProperty("GapToLeader", out var gapProp) ? gapProp.GetString() ?? "" : "";
                        string bestLap = driverData.TryGetProperty("BestLapTime", out var lapProp) ? lapProp.GetString() ?? "-" : "-";

                        if (!string.IsNullOrEmpty(tla))
                        {
                            standingsList.Add(new DriverStandingModel
                            {
                                Position = pos,
                                Tla = tla,
                                FullName = fullName,
                                Gap = string.IsNullOrEmpty(gap) ? "LEADER" : gap,
                                BestLap = bestLap
                            });
                        }
                    }

                    if (standingsList.Count > 0)
                    {
                        _state.Standings = standingsList.OrderBy(d => d.Position).ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing MultiViewer GraphQL payload.");
        }
    }
}