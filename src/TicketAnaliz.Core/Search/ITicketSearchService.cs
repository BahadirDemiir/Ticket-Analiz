using TicketAnaliz.Core.Entities;

namespace TicketAnaliz.Core.Search;

public record TicketSearchResult(Ticket Ticket, float Score);

public interface ITicketSearchService
{
    Task<IReadOnlyList<TicketSearchResult>> SearchAsync(string queryText, int topK = 5, CancellationToken ct = default);
}
