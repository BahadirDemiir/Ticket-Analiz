using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Core.Rag;

// Answer == null: web yolu vazgecti (sorgu uretilemedi / alakali sonuc yok / kaynaklarda cozum yok),
// cagiran BT'ye yonlendirmeye devam eder. Trace her durumda dolu.
public record WebFallbackOutcome(
    string? Answer,
    IReadOnlyList<WebSearchResult> Sources,
    HallucinationCheckResult? HallucinationCheck,
    TimeSpan? GenerationDuration,
    TimeSpan? HallucinationCheckDuration,
    WebSearchTrace Trace)
{
    public static WebFallbackOutcome GaveUp(WebSearchTrace trace, TimeSpan? generationDuration = null) =>
        new(null, Array.Empty<WebSearchResult>(), null, generationDuration, null, trace);
}

public interface IWebFallbackService
{
    Task<WebFallbackOutcome> TryAsync(string queryText, CancellationToken ct = default);
}
