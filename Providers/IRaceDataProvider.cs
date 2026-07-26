using LTCrawlerSSR.Models;

namespace LTCrawlerSSR.Providers;

public interface IRaceDataProvider
{
    string SeriesName { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    RaceStateModel GetCurrentState();
}