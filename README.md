# LTCrawlerSSR

**LTCrawlerSSR** is a high-performance, real-time Server-Side Rendered (SSR) backend built with **.NET 10** designed specifically for live streamers covering motorsport events (Formula 1, Formula 2, and F1 Academy).

It aggregates live timing, telemetry, weather conditions, and track statuses from official streams (MultiViewer for F1 and FIA SignalR WebSocket endpoints) and renders fully styled HTML broadcast overlays dynamically. It integrates seamlessly with **Meld Studio** as browser sources and responds to chat commands via **Streamer.bot**.

---

## Architecture & Features

* **Server-Side Rendering (SSR):** Renders complete HTML layouts directly on the server with zero client-side JavaScript bundle overhead, ensuring ultra-low latency for live broadcast production.
* **Multi-Series Support:** Automatically normalizes data structures across **Formula 1** (via local MultiViewer instance), **Formula 2**, and **F1 Academy** (via direct FIA SignalR WebSockets).
* **Dual Overlay Options:**
* `/overlay`: A compact top-5 standings ticker designed for multi-tasking streams or when focusing on other content.
* `/scene`: A complete widescreen television-style broadcast scene featuring live standings, weather metrics (air/track temp, wind speed), and track condition badges for dedicated race coverage.


* **Chat Command Orchestration:** Connects with **Streamer.bot** via HTTP endpoints to switch active data feeds and styling on the fly (`!f1`, `!f2`, `!academy`).

---

## Project Structure

```text
LTCrawlerSSR/
│
├── Models/
│   └── RaceModels.cs            <-- Shared state store and data transfer objects
│
├── Providers/
│   ├── IRaceDataProvider.cs     <-- Common interface for telemetry providers
│   ├── MultiViewerF1Provider.cs <-- Local polling provider for F1 MultiViewer API
│   └── FiaSignalRProvider.cs    <-- SignalR WebSocket client for F2 / F1 Academy
│
├── Services/
│   └── RaceSessionOrchestrator.cs <-- Manages runtime switching between racing series
│
└── Program.cs                   <-- Minimal API routing, SSR layout engine, and endpoints

```

---

## Prerequisites

* **.NET 10 SDK** installed on your machine.
* **MultiViewer for F1** running locally (if streaming F1 sessions).
* **Streamer.bot** configured for your streaming platforms (Twitch, YouTube, Kick).
* **Meld Studio** for managing stream layouts and browser sources.

---

## Getting Started

1. **Clone or Open the Project** in your local terminal or IDE (e.g., Visual Studio / VS Code).
2. **Install Required NuGet Packages** (ensure the SignalR client package is present):
```bash
dotnet add package Microsoft.AspNetCore.SignalR.Client

```


3. **Run the Application:**
```dotnetcli
dotnet run

```


The backend will start locally on `http://localhost:5000`.

---

## Integrating with Meld Studio

Add two **Browser Sources** inside Meld Studio depending on your broadcast setup:

* **Compact Ticker (When streaming other content):**
* **URL:** `http://localhost:5000/overlay`


* **Full Broadcast Scene (When focused on live races):**
* **URL:** `http://localhost:5000/scene`



*(Both endpoints include auto-refresh logic, ensuring real-time DOM updates directly from the server).*

---

## Streamer.bot Integration

To switch championships on the fly using chat commands:

1. In **Streamer.bot**, navigate to **Commands** and set up triggers (e.g., `!f1`, `!f2`, `!academy`).
2. Add a sub-action to send an **HTTP POST Request**:
* **URL:** `http://localhost:5000/api/switch-feed`
* **Payload (JSON):**
```json
{
  "series": "F2",
  "connectionToken": "YOUR_SESSION_CONNECTION_TOKEN_HERE"
}

```




*(For F1, the token field can be left blank since it polls MultiViewer locally).*