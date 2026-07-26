using MyRaceBackend.Models;

namespace MyRaceBackend.Providers;

public interface IRaceDataProvider
{
    string SeriesName { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    RaceStateModel GetCurrentState();
}