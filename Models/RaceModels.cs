namespace LTCrawlerSSR.Models;

public class RaceStateModel 
{
    public string Series { get; set; } = "F1";
    public string Circuit { get; set; } = "Live Track";
    public string Session { get; set; } = "Session Active";
    public string TrackStatus { get; set; } = "GREEN";
    public string AirTemp { get; set; } = "--";
    public string TrackTemp { get; set; } = "--";
    public string WindSpeed { get; set; } = "--";
    public List<DriverStandingModel> Standings { get; set; } = new();
}

public class DriverStandingModel 
{
    public int Position { get; set; }
    public string Tla { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Gap { get; set; } = "";
    public string BestLap { get; set; } = "";
}

public record FeedSwitchRequest(string Series, string ConnectionToken);