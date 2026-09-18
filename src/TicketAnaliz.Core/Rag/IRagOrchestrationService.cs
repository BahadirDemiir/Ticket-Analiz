using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Core.Rag;

public record RagTrace(
    TimeSpan SearchDuration,
    TimeSpan? GenerationDuration,
    TimeSpan? HallucinationCheckDuration,
    TimeSpan TotalDuration);

public record RagSuggestionResult(
    string Answer,
    IReadOnlyList<TicketSearchResult> Sources,
    ConfidenceResult Confidence,
    HallucinationCheckResult? HallucinationCheck,
    RagTrace Trace);

public interface IRagOrchestrationService
{
    Task<RagSuggestionResult> GenerateSuggestionAsync(string queryText, CancellationToken ct = default);
}
