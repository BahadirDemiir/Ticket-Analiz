using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Infrastructure.Search;

public class TavilyWebSearchService : IWebSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TavilyWebSearchService> _logger;
    private readonly string _apiKey;
    private readonly int _maxResults;

    public TavilyWebSearchService(HttpClient httpClient, IConfiguration configuration, ILogger<TavilyWebSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Tavily:ApiKey"] ?? string.Empty;
        _maxResults = int.TryParse(configuration["Tavily:MaxResults"], out var max) ? max : 5;
    }

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Tavily:ApiKey tanimli degil, web aramasi atlaniyor.");
            return Array.Empty<WebSearchResult>();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search")
            {
                Content = JsonContent.Create(new TavilyRequest(query, "basic", _maxResults))
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<TavilyResponse>(cancellationToken: ct);

            return body?.Results?
                .Select(r => new WebSearchResult(r.Title ?? "", r.Url ?? "", r.Content ?? "", r.Score))
                .ToList()
                ?? new List<WebSearchResult>();
        }
        catch (HttpRequestException ex)
        {
            // Web aramasi sadece yedek yol: patlarsa butun istegi dusurmeyelim, BT'ye yonlendirme kalsin.
            _logger.LogWarning(ex, "Tavily web aramasi basarisiz oldu.");
            return Array.Empty<WebSearchResult>();
        }
    }

    private record TavilyRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("search_depth")] string SearchDepth,
        [property: JsonPropertyName("max_results")] int MaxResults);

    private class TavilyResponse
    {
        [JsonPropertyName("results")]
        public List<TavilyResult>? Results { get; set; }
    }

    private class TavilyResult
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("score")] public double Score { get; set; }
    }
}
