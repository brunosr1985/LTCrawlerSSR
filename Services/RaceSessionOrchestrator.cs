using Microsoft.Extensions.DependencyInjection;
using LTCrawlerSSR.Models;
using LTCrawlerSSR.Providers;

namespace LTCrawlerSSR.Services;

public class RaceSessionOrchestrator
{
    private IRaceDataProvider? _activeProvider;
    private readonly IServiceProvider _serviceProvider;

    public RaceSessionOrchestrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        
        // Automatically default to F1 on startup
        _ = InitializeDefaultAsync();
    }

    private async Task InitializeDefaultAsync()
    {
        await SwitchToF1Async();
    }

    public async Task SwitchToFiaAsync(string seriesName, string hubUrl, string connectionToken)
    {
        if (_activeProvider != null)
        {
            await _activeProvider.StopAsync();
        }

        var fiaProvider = _serviceProvider.GetRequiredService<FiaSignalRProvider>();
        fiaProvider.ConfigureTarget(seriesName, hubUrl, connectionToken);
        
        _activeProvider = fiaProvider;
        await _activeProvider.StartAsync(CancellationToken.None);
    }

    public async Task SwitchToF1Async()
    {
        if (_activeProvider != null)
        {
            await _activeProvider.StopAsync();
        }

        var f1Provider = _serviceProvider.GetRequiredService<MultiViewerF1Provider>();
        
        _activeProvider = f1Provider;
        await _activeProvider.StartAsync(CancellationToken.None);
    }
    public RaceStateModel GetState()
    {
        // Ele tenta pegar o estado do provedor que estiver rodando no momento
        // Se não houver provedor ativo, retorna um estado padrão com "NONE"
        return _activeProvider?.GetCurrentState() ?? new RaceStateModel { Series = "NONE" };
    }
}