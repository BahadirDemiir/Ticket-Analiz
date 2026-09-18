using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Core.Rag;

public record RagSuggestionResult(string Answer, IReadOnlyList<TicketSearchResult> Sources, ConfidenceResult Confidence);

public interface IRagOrchestrationService
{
    Task<RagSuggestionResult> GenerateSuggestionAsync(string queryText, CancellationToken ct = default);
}
