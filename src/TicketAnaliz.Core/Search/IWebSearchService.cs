namespace TicketAnaliz.Core.Search;

public record WebSearchResult(string Title, string Url, string Content, double Score);

public interface IWebSearchService
{
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, CancellationToken ct = default);
}
