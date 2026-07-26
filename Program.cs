using MyRaceBackend.Models;
using MyRaceBackend.Services;
using MyRaceBackend.Providers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MultiViewerF1Provider>();
builder.Services.AddSingleton<RaceSessionOrchestrator>();

var app = builder.Build();

// Endpoint SSE para o Overlay (Stream contínuo)
app.MapGet("/stream-race", async (RaceSessionOrchestrator orchestrator, HttpContext context, CancellationToken ct) =>
{
    context.Response.ContentType = "text/event-stream";
    while (!ct.IsCancellationRequested)
    {
        var json = JsonSerializer.Serialize(orchestrator.GetState());
        await context.Response.WriteAsync($"data: {json}\n\n", ct);
        await context.Response.Body.FlushAsync(ct);
        await Task.Delay(1000, ct);
    }
});

// Endpoint da Cena (HTML estático que conecta ao SSE)
app.MapGet("/scene", async (HttpContext context) => { 
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync("wwwroot/scene.html");
});

app.Run("http://localhost:5000");