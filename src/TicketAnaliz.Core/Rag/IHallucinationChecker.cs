using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Core.Rag;

public record HallucinationCheckResult(bool HasUnsupportedClaims, string Explanation);

public interface IHallucinationChecker
{
    Task<HallucinationCheckResult> CheckAsync(string answer, IReadOnlyList<TicketSearchResult> sources, CancellationToken ct = default);

    Task<HallucinationCheckResult> CheckWebAsync(string answer, IReadOnlyList<WebSearchResult> sources, CancellationToken ct = default);
}
