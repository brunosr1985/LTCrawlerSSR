using System.Net.Http.Json;
using System.Text.Json;
using LTCrawlerSSR.Models;
        
namespace LTCrawlerSSR.Providers
{
    public class MultiViewerF1Provider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MultiViewerF1Provider> _logger;

        public MultiViewerF1Provider(HttpClient httpClient, ILogger<MultiViewerF1Provider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<F1LiveTimingState?> GetLiveTimingStateAsync()
        {
            try
            {
                // Adjust your GraphQL query payload pattern matching your current setup
                var graphQlQuery = new
                {
                    query = "{ f1LiveTimingState { TimingData { Lines Withheld } WeatherData { AirTemp TrackTemp Humidity } TrackStatus { Status Message } SessionInfo { Name Meeting { Name } } } }"
                };

                var response = await _httpClient.PostAsJsonAsync("/graphql", graphQlQuery);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch MultiViewer data. Status code: {StatusCode}", response.StatusCode);
                    return null;
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = await response.Content.ReadFromJsonAsync<MultiViewerRootResponse>(jsonOptions);
                return result?.Data?.F1LiveTimingState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching data from MultiViewer GraphQL endpoint.");
                return null;
            }
        }
    }
}