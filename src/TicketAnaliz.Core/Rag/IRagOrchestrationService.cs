using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Core.Rag;

public enum AnswerSource
{
    HistoricalTickets,
    WebSearch,
    Escalated
}

// Web aramasinin ara adimlari: sistem izi paneli ve evaluation icin.
public record WebSearchTrace(
    string? RewrittenQuery,
    int ResultCount,
    int AcceptedCount,
    double? TopScore,
    TimeSpan Duration);

public record RagTrace(
    TimeSpan SearchDuration,
    TimeSpan? GenerationDuration,
    TimeSpan? HallucinationCheckDuration,
    TimeSpan TotalDuration,
    WebSearchTrace? WebSearch = null);

// Sources: her zaman gecmis ticket aramasinin sonucu (web'e dusuldugunde bile zayif adaylar
// izleme icin burada kalir). WebSources sadece Source == WebSearch iken dolu.
public record RagSuggestionResult(
    string Answer,
    IReadOnlyList<TicketSearchResult> Sources,
    ConfidenceResult Confidence,
    HallucinationCheckResult? HallucinationCheck,
    RagTrace Trace,
    AnswerSource Source = AnswerSource.HistoricalTickets,
    IReadOnlyList<WebSearchResult>? WebSources = null);

public interface IRagOrchestrationService
{
    // allowWebFallback=false: gecmis kayit yetersizse internete dusme (evaluation olcumunu karistirmasin diye).
    Task<RagSuggestionResult> GenerateSuggestionAsync(string queryText, bool allowWebFallback = true, CancellationToken ct = default);
}
