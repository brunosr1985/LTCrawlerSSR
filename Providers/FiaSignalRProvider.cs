using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using MyRaceBackend.Models;

namespace MyRaceBackend.Providers;

public class FiaSignalRProvider : IRaceDataProvider
{
    private HubConnection? _connection;
    private readonly ILogger<FiaSignalRProvider> _logger;
    private readonly RaceStateModel _state;
    private string _hubUrl = "";
    private string _connectionToken = "";

    public string SeriesName { get; private set; } = "F2";

    public FiaSignalRProvider(ILogger<FiaSignalRProvider> logger, RaceStateModel state)
    {
        _logger = logger;
        _state = state;
    }

    public void ConfigureTarget(string seriesName, string hubUrl, string connectionToken)
    {
        SeriesName = seriesName;
        _state.Series = seriesName;
        _hubUrl = hubUrl;
        _connectionToken = connectionToken;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_connection != null)
        {
            await _connection.StopAsync(cancellationToken);
        }

        _connection = new HubConnectionBuilder()
            .WithUrl($"{_hubUrl}/start?transport=webSockets&clientProtocol=2.1&connectionToken={Uri.EscapeDataString(_connectionToken)}&connectionData=[{{\"name\":\"streaming\"}}]")
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, object>("feed", (topic, payload) =>
        {
            ProcessLiveFeed(topic, payload);
        });

        await _connection.StartAsync(cancellationToken);
        _logger.LogInformation("Connected live to {series} SignalR feed.", SeriesName);
    }

    public async Task StopAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            _logger.LogInformation("Disconnected from {series} SignalR feed.", SeriesName);
        }
    }

    public RaceStateModel GetCurrentState() => _state;

    private void ProcessLiveFeed(string topic, object payload)
    {
        if (payload is not JsonElement jsonElement) return;

        try
        {
            if (topic == "data")
            {
                var standings = new List<DriverStandingModel>();
                foreach (var prop in jsonElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object &&
                        prop.Value.TryGetProperty("driver", out var driverObj) &&
                        prop.Value.TryGetProperty("position", out var posObj))
                    {
                        int pos = int.TryParse(posObj.GetProperty("Value").GetString(), out var p) ? p : 99;
                        string fullName = driverObj.GetProperty("FullName").GetString() ?? "";
                        string tla = driverObj.GetProperty("TLA").GetString() ?? "";
                        string gap = prop.Value.TryGetProperty("gap", out var gObj) && gObj.TryGetProperty("Value", out var gVal) ? gVal.GetString() ?? "" : "";
                        string best = prop.Value.TryGetProperty("best", out var bObj) && bObj.TryGetProperty("Value", out var bVal) ? bVal.GetString() ?? "-" : "-";

                        standings.Add(new DriverStandingModel { Position = pos, FullName = fullName, Tla = tla, Gap = string.IsNullOrEmpty(gap) ? "LEADER" : gap, BestLap = best });
                    }
                }
                if (standings.Count > 0)
                {
                    _state.Standings = standings.OrderBy(d => d.Position).ToList();
                }
            }
            else if (topic == "weatherfeed")
            {
                if (jsonElement.TryGetProperty("airtemp", out var air)) _state.AirTemp = air.GetString() ?? "--";
                if (jsonElement.TryGetProperty("tracktemp", out var trk)) _state.TrackTemp = trk.GetString() ?? "--";
                if (jsonElement.TryGetProperty("windspeed", out var wnd)) _state.WindSpeed = wnd.GetString() ?? "--";
            }
            else if (topic == "trackfeed")
            {
                if (jsonElement.TryGetProperty("Message", out var msg)) _state.TrackStatus = msg.GetString() ?? "GREEN";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing live FIA feed packet.");
        }
    }
}