using System.Text.Json;
using MyRaceBackend.Models;
using MyRaceBackend.Providers;
using MyRaceBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// Register state container, providers, and orchestrator
builder.Services.AddSingleton<RaceStateModel>();
builder.Services.AddSingleton<MultiViewerF1Provider>();
builder.Services.AddSingleton<FiaSignalRProvider>();
builder.Services.AddSingleton<RaceSessionOrchestrator>();
builder.Services.AddHttpClient();

var app = builder.Build();

// 1. Streamer.bot / Chat Command Hook to switch active live series dynamically
app.MapPost("/api/switch-feed", async (FeedSwitchRequest req, RaceSessionOrchestrator orchestrator) =>
{
    string seriesUpper = req.Series.ToUpper();

    if (seriesUpper == "F2" || seriesUpper == "F1_ACADEMY")
    {
        string hubUrl = seriesUpper == "F2" 
            ? "https://ltss.fiaformula2.com/streaming" 
            : "https://f2f3-prod-livetiming.azurewebsites.net/streaming";

        // Switch to FIA SignalR provider and configure target
        await orchestrator.SwitchToFiaAsync(seriesUpper, hubUrl, req.ConnectionToken);
    }
    else
    {
        // Switch to MultiViewer F1 provider
        await orchestrator.SwitchToF1Async();
    }

    return Results.Ok(new { status = "Switched to " + req.Series });
});

// 2. ENDPOINT: /overlay (Compact Ticker for multi-tasking / streaming other content)
app.MapGet("/overlay", async (RaceStateModel state, HttpContext context) =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    string accentColor = state.Series == "F2" ? "#0055ff" : state.Series == "F1_ACADEMY" ? "#b000ff" : "#ff3b30";

    var html = $@"
    <!DOCTYPE html>
    <html lang=""en"">
    <head>
        <meta charset=""UTF-8"">
        <title>Compact Race Overlay</title>
        <meta http-equiv=""refresh"" content=""1"">
        <style>
            body {{
                margin: 0;
                padding: 15px;
                background: transparent;
                font-family: 'Segoe UI', Tahoma, sans-serif;
                color: #ffffff;
            }}
            .compact-box {{
                width: 350px;
                background: rgba(12, 12, 20, 0.9);
                border-left: 5px solid {accentColor};
                border-radius: 6px;
                padding: 12px;
                box-shadow: 0 8px 24px rgba(0,0,0,0.6);
                backdrop-filter: blur(4px);
            }}
            .header {{
                display: flex;
                justify-content: space-between;
                align-items: center;
                border-bottom: 1px solid rgba(255,255,255,0.15);
                padding-bottom: 6px;
                margin-bottom: 8px;
            }}
            .header h3 {{ margin: 0; font-size: 14px; text-transform: uppercase; letter-spacing: 1px; }}
            .series-badge {{ background: {accentColor}; padding: 2px 6px; border-radius: 3px; font-size: 10px; font-weight: bold; }}
            .driver-row {{
                display: flex;
                justify-content: space-between;
                padding: 4px 0;
                font-size: 13px;
                border-bottom: 1px solid rgba(255,255,255,0.04);
            }}
            .pos {{ font-weight: bold; color: #ffcc00; width: 25px; }}
            .tla {{ font-weight: bold; }}
            .gap {{ color: #00ffcc; font-size: 12px; font-family: monospace; }}
        </style>
    </head>
    <body>
        <div class=""compact-box"">
            <div class=""header"">
                <h3>{state.Series} Standings</h3>
                <span class=""series-badge"">{state.Series}</span>
            </div>";

    if (state.Standings.Count == 0)
    {
        html += "<div style=\"color: #777; font-size:12px; padding: 6px;\">Connecting to live feed...</div>";
    }
    else
    {
        foreach (var d in state.Standings.Take(5))
        {
            html += $@"
                <div class=""driver-row"">
                    <div><span class=""pos"">{d.Position}.</span> <span class=""tla"">{d.Tla}</span></div>
                    <div class=""gap"">{d.Gap}</div>
                </div>";
        }
    }

    html += "</div></body></html>";
    await context.Response.WriteAsync(html);
});
// Endpoint que "empurra" dados para o Overlay via SSE
app.MapGet("/stream-race", async (RaceSessionOrchestrator orchestrator, HttpContext context, CancellationToken ct) =>
{
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    while (!ct.IsCancellationRequested)
    {
        var state = orchestrator.GetState();
        var json = JsonSerializer.Serialize(state);
        
        // Formato padrão SSE: "data: {json}\n\n"
        await context.Response.WriteAsync($"data: {json}\n\n", ct);
        await context.Response.Body.FlushAsync(ct);

        // Aguarda 1 segundo antes do próximo push para não sobrecarregar
        await Task.Delay(1000, ct);
    }
});

app.MapGet("/scene", async (RaceStateModel state, HttpContext context) =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    string accentColor = state.Series == "F2" ? "#0055ff" : state.Series == "F1_ACADEMY" ? "#b000ff" : "#ff3b30";

    var html = $@"
    <!DOCTYPE html>
    <html lang=""en"">
    <head>
        <meta charset=""UTF-8"">
        <title>Adaptive Full Broadcast Scene</title>
        <meta http-equiv=""refresh"" content=""1"">
        <style>
            * {{ box-sizing: border-box; }}
            body {{
                margin: 0;
                padding: 0;
                width: 100vw;
                height: 100vh;
                background: transparent;
                font-family: 'Segoe UI', Roboto, sans-serif;
                color: #ffffff;
                overflow: hidden;
            }}

            /* Fluid 16:9 Grid Layout */
            .fullscreen-layout {{
                display: grid;
                grid-template-columns: 22vw 1fr 20vw;  /* Standings Tower | Main Content | Chat Space */
                grid-template-rows: 7vh 1fr;           /* Header Bar | Core Content Area */
                gap: 1.2vw;
                padding: 1.5vw;
                width: 100vw;
                height: 100vh;
            }}

            /* Glassmorphism Panel */
            .panel {{
                background: rgba(10, 10, 16, 0.88);
                border: 1px solid rgba(255, 255, 255, 0.12);
                border-radius: 0.8vh;
                backdrop-filter: blur(8px);
                box-shadow: 0 1.2vh 4vh rgba(0,0,0,0.7);
                overflow: hidden;
            }}

            /* 1. TOP HEADER BAR */
            .header-panel {{
                grid-column: 1 / -1;
                grid-row: 1;
                display: flex;
                justify-content: space-between;
                align-items: center;
                padding: 0 1.5vw;
                border-left: 0.5vw solid {accentColor};
                background: linear-gradient(90deg, rgba(0,85,255,0.25) 0%, rgba(10,10,16,0.95) 40%);
            }}
            .header-title h1 {{ margin: 0; font-size: 1.2vw; letter-spacing: 0.1vw; text-transform: uppercase; }}
            .header-title span {{ font-size: 0.8vw; color: #88aaff; text-transform: uppercase; font-weight: bold; }}
            
            .badge-group {{ display: flex; gap: 0.8vw; align-items: center; }}
            .status-badge {{ background: #00aa55; color: #fff; padding: 0.4vh 0.8vw; border-radius: 0.4vh; font-weight: bold; font-size: 0.8vw; letter-spacing: 0.05vw; }}
            .weather-badge {{ background: rgba(255,255,255,0.1); padding: 0.4vh 0.8vw; border-radius: 0.4vh; font-size: 0.8vw; }}

            /* 2. LEFT COLUMN: STANDINGS TOWER */
            .standings-panel {{
                grid-column: 1;
                grid-row: 2;
                display: flex;
                flex-direction: column;
                padding: 1vh 1vw;
            }}
            .panel-title {{
                font-size: 0.75vw;
                color: #888;
                text-transform: uppercase;
                letter-spacing: 0.1vw;
                margin-bottom: 1vh;
                border-bottom: 1px solid rgba(255,255,255,0.1);
                padding-bottom: 0.5vh;
                font-weight: bold;
            }}
            .standings-list {{
                overflow-y: auto;
                flex-grow: 1;
            }}
            .driver-row {{
                display: flex;
                align-items: center;
                justify-content: space-between;
                padding: 0.8vh 0.5vw;
                border-bottom: 1px solid rgba(255,255,255,0.05);
                font-size: 0.85vw;
            }}
            .pos {{ font-weight: 800; color: #ffcc00; width: 1.8vw; }}
            .tla {{ font-weight: 700; color: #fff; width: 3vw; }}
            .name {{ flex-grow: 1; color: #ccc; font-size: 0.8vw; }}
            .gap {{ color: #00ffcc; font-size: 0.75vw; font-family: monospace; }}

            /* 3. CENTER COLUMN: MAIN CONTENT */
            .center-stage {{
                grid-column: 2;
                grid-row: 2;
                position: relative;
                display: flex;
                flex-direction: column;
                justify-content: flex-end;
                align-items: flex-start;
                padding: 1vw;
                pointer-events: none;
            }}

            /* PERFECT 16:9 CAMERA FRAME BOX */
            .camera-frame-area {{
                pointer-events: auto;
                width: 24vw;
                aspect-ratio: 16 / 9;
                border: 2px dashed rgba(255, 255, 255, 0.4);
                border-radius: 0.8vh;
                background: rgba(0, 0, 0, 0.4);
                display: flex;
                align-items: flex-end;
                padding: 0.8vh 0.8vw;
                box-shadow: 0 0.8vh 2.5vh rgba(0,0,0,0.6);
            }}
            .framing-label {{
                font-size: 0.65vw;
                text-transform: uppercase;
                letter-spacing: 0.05vw;
                color: rgba(255,255,255,0.6);
                background: rgba(0,0,0,0.7);
                padding: 0.3vh 0.5vw;
                border-radius: 0.3vh;
            }}

            /* 4. RIGHT COLUMN: LIVE CHAT & METRICS AREA */
            .chat-and-metrics-area {{
                grid-column: 3;
                grid-row: 2;
                display: flex;
                flex-direction: column;
                gap: 1.2vh;
                padding: 1vh 1vw;
            }}
            .metrics-card-container {{
                display: grid;
                grid-template-columns: repeat(3, 1fr);
                gap: 0.5vw;
            }}
            .metric-card {{
                background: rgba(255,255,255,0.04);
                border: 1px solid rgba(255,255,255,0.08);
                padding: 0.8vh;
                border-radius: 0.5vh;
                text-align: center;
            }}
            .metric-card label {{ display: block; font-size: 0.6vw; color: #888; text-transform: uppercase; margin-bottom: 0.2vh; }}
            .metric-card value {{ font-size: 0.95vw; font-weight: bold; color: #fff; }}

            .chat-placeholder-box {{
                flex-grow: 1;
                border: 2px dashed rgba(255, 255, 255, 0.2);
                border-radius: 0.6vh;
                background: rgba(0, 0, 0, 0.2);
                display: flex;
                align-items: center;
                justify-content: center;
                text-align: center;
                padding: 1vw;
                color: rgba(255,255,255,0.4);
                font-size: 0.8vw;
                text-transform: uppercase;
                letter-spacing: 0.05vw;
                line-height: 1.4;
            }}
        </style>
    </head>
    <body>
        <div class=""fullscreen-layout"">
            
            <div class=""panel header-panel"">
                <div class=""header-title"">
                    <span>{state.Series} Broadcast Session</span>
                    <h1>{state.Circuit}</h1>
                </div>
                <div class=""badge-group"">
                    <div class=""status-badge"">{state.TrackStatus}</div>
                    <div class=""weather-badge"">Air: {state.AirTemp}&deg;C | Track: {state.TrackTemp}&deg;C | Wind: {state.WindSpeed}m/s</div>
                </div>
            </div>

            <div class=""panel standings-panel"">
                <div class=""panel-title"">Live Standings Tower</div>
                <div class=""standings-list"">";

    if (state.Standings.Count == 0)
    {
        html += "<div style=\"color: #777; text-align:center; padding: 2vh;\">Waiting for feed sync...</div>";
    }
    else
    {
        foreach (var d in state.Standings)
        {
            html += $@"
                    <div class=""driver-row"">
                        <span class=""pos"">{d.Position}</span>
                        <span class=""tla"">{d.Tla}</span>
                        <span class=""name"">{d.FullName}</span>
                        <span class=""gap"">{d.Gap}</span>
                    </div>";
        }
    }

    html += $@"
                </div>
            </div>

            <div class=""center-stage"">
                <div class=""camera-frame-area"">
                    <div class=""framing-label"">[ Camera Frame - 16:9 ]</div>
                </div>
            </div>

            <div class=""panel chat-and-metrics-area"">
                <div class=""panel-title"" style=""margin:0;"">Session Telemetry</div>
                <div class=""metrics-card-container"">
                    <div class=""metric-card""><label>Air</label><value>{state.AirTemp}&deg;</value></div>
                    <div class=""metric-card""><label>Track</label><value>{state.TrackTemp}&deg;</value></div>
                    <div class=""metric-card""><label>Wind</label><value>{state.WindSpeed}</value></div>
                </div>

                <div class=""panel-title"" style=""margin-top:0.5vh;"">Stream Chat Area</div>
                <div class=""chat-placeholder-box"">
                    Place your Multi-Platform<br>Chat Overlay Here
                </div>
            </div>

        </div>
    </body>
    </html>";

    await context.Response.WriteAsync(html);
});

app.Run("http://localhost:5000");

record FeedSwitchRequest(string Series, string ConnectionToken);