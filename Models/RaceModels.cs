using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LTCrawlerSSR.Models
{
    public class MultiViewerRootResponse
    {
        [JsonPropertyName("data")]
        public MultiViewerData? Data { get; set; }
    }

    public class MultiViewerData
    {
        [JsonPropertyName("f1LiveTimingState")]
        public F1LiveTimingState? F1LiveTimingState { get; set; }
    }

    public class F1LiveTimingState
    {
        [JsonPropertyName("TimingData")]
        public TimingDataContainer? TimingData { get; set; }

        [JsonPropertyName("WeatherData")]
        public WeatherData? WeatherData { get; set; }

        [JsonPropertyName("TrackStatus")]
        public TrackStatus? TrackStatus { get; set; }

        [JsonPropertyName("SessionInfo")]
        public SessionInfo? SessionInfo { get; set; }
    }

    public class TimingDataContainer
    {
        [JsonPropertyName("Lines")]
        public Dictionary<string, TimingLine> Lines { get; set; } = new();

        [JsonPropertyName("Withheld")]
        public bool Withheld { get; set; }
    }

    public class TimingLine
    {
        [JsonPropertyName("Position")]
        public string? Position { get; set; }

        [JsonPropertyName("RacingNumber")]
        public string? RacingNumber { get; set; }

        [JsonPropertyName("Line")]
        public int Line { get; set; }

        [JsonPropertyName("Retired")]
        public bool Retired { get; set; }

        [JsonPropertyName("InPit")]
        public bool InPit { get; set; }

        [JsonPropertyName("GapToLeader")]
        public string? GapToLeader { get; set; }

        [JsonPropertyName("BestLapTime")]
        public LapTimeInfo? BestLapTime { get; set; }

        [JsonPropertyName("LastLapTime")]
        public LastLapTimeInfo? LastLapTime { get; set; }

        [JsonPropertyName("Sectors")]
        public List<SectorInfo> Sectors { get; set; } = new();

        [JsonPropertyName("Speeds")]
        public Dictionary<string, SpeedInfo> Speeds { get; set; } = new();
    }

    public class LapTimeInfo
    {
        [JsonPropertyName("Value")]
        public string? Value { get; set; }

        [JsonPropertyName("Lap")]
        public int Lap { get; set; }
    }

    public class LastLapTimeInfo
    {
        [JsonPropertyName("Value")]
        public string? Value { get; set; }

        [JsonPropertyName("PersonalFastest")]
        public bool PersonalFastest { get; set; }

        [JsonPropertyName("OverallFastest")]
        public bool OverallFastest { get; set; }
    }

    public class SectorInfo
    {
        [JsonPropertyName("Value")]
        public string? Value { get; set; }

        [JsonPropertyName("PreviousValue")]
        public string? PreviousValue { get; set; }

        [JsonPropertyName("PersonalFastest")]
        public bool PersonalFastest { get; set; }

        [JsonPropertyName("OverallFastest")]
        public bool OverallFastest { get; set; }
    }

    public class SpeedInfo
    {
        [JsonPropertyName("Value")]
        public string? Value { get; set; }

        [JsonPropertyName("PersonalFastest")]
        public bool PersonalFastest { get; set; }

        [JsonPropertyName("OverallFastest")]
        public bool OverallFastest { get; set; }
    }

    public class WeatherData
    {
        [JsonPropertyName("AirTemp")]
        public string? AirTemp { get; set; }

        [JsonPropertyName("TrackTemp")]
        public string? TrackTemp { get; set; }

        [JsonPropertyName("Humidity")]
        public string? Humidity { get; set; }
    }

    public class TrackStatus
    {
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        [JsonPropertyName("Message")]
        public string? Message { get; set; }
    }

    public class SessionInfo
    {
        [JsonPropertyName("Meeting")]
        public Meeting? Meeting { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }
    }

    public class Meeting
    {
        [JsonPropertyName("Name")]
        public string? Name  { get; set; }
    }
}