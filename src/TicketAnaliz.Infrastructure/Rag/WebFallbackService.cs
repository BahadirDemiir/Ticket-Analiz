using System.Diagnostics;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TicketAnaliz.Core.Rag;
using TicketAnaliz.Core.Search;
using TicketAnaliz.Infrastructure.Search;

namespace TicketAnaliz.Infrastructure.Rag;

// Gecmis kayitlar yetersizken devreye giren internet arastirmasi:
// sikayet -> arama sorgusu -> web sonuclari -> skor esigi -> alaka suzgeci -> cevap -> hallucination kontrolu.
// Her asamada "vazgecebilir": bu durumda Answer null doner ve cagiran BT'ye yonlendirmeye devam eder.
public class WebFallbackService : IWebFallbackService
{
    // Tavily skoru bunun altindaysa sonuc alakasiz sayilir. Adim 2 testinde isabetli sonuc 0.80,
    // cop sonuclar 0.27 ve altindaydi.
    private const double MinimumWebScore = 0.5;

    private readonly IWebSearchService _webSearchService;
    private readonly WebQueryRewriter _webQueryRewriter;
    private readonly WebRelevanceFilter _relevanceFilter;
    private readonly RagPromptBuilder _promptBuilder;
    private readonly IHallucinationChecker _hallucinationChecker;
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public WebFallbackService(
        IWebSearchService webSearchService,
        WebQueryRewriter webQueryRewriter,
        WebRelevanceFilter relevanceFilter,
        RagPromptBuilder promptBuilder,
        IHallucinationChecker hallucinationChecker,
        Kernel kernel)
    {
        _webSearchService = webSearchService;
        _webQueryRewriter = webQueryRewriter;
        _relevanceFilter = relevanceFilter;
        _promptBuilder = promptBuilder;
        _hallucinationChecker = hallucinationChecker;
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<WebFallbackOutcome> TryAsync(string queryText, CancellationToken ct = default)
    {
        var webStopwatch = Stopwatch.StartNew();

        var rewrittenQuery = await _webQueryRewriter.RewriteAsync(queryText, ct);
        if (rewrittenQuery is null)
        {
            webStopwatch.Stop();
            return WebFallbackOutcome.GaveUp(new WebSearchTrace(null, 0, 0, null, webStopwatch.Elapsed));
        }

        var results = await _webSearchService.SearchAsync(rewrittenQuery, ct);
        var aboveThreshold = results.Where(r => r.Score >= MinimumWebScore).OrderByDescending(r => r.Score).ToList();

        // Skor tek basina yetmiyor: farkli bir konuda ama benzer kelimeli sonuclar da yuksek skor alabiliyor
        // (ornegin SAP ekran yetkisi sorusuna "HTTP 401 Unauthorized" sonuclari). LLM her sonucun kullanicinin
        // sorun turune yardimci olup olmadigina karar veriyor. Gecmis ticket'lardaki katı RerankingService degil,
        // web'e ozel gevsek suzgec kullaniliyor (katı olan isabetli web sonuclarini da eliyordu).
        var accepted = new List<WebSearchResult>();
        if (aboveThreshold.Count > 0)
        {
            var candidates = aboveThreshold
                .Select((r, i) => new RerankCandidate(i, r.Title, r.Content.Length > 600 ? r.Content[..600] : r.Content))
                .ToList();
            var relevantIndices = await _relevanceFilter.GetRelevantIndicesAsync(queryText, candidates, ct);
            accepted = aboveThreshold.Where((_, i) => relevantIndices.Contains(i)).ToList();
        }
        webStopwatch.Stop();

        var webTrace = new WebSearchTrace(
            rewrittenQuery,
            results.Count,
            accepted.Count,
            results.Count > 0 ? results.Max(r => r.Score) : null,
            webStopwatch.Elapsed);

        if (accepted.Count == 0)
        {
            return WebFallbackOutcome.GaveUp(webTrace);
        }

        var prompt = _promptBuilder.BuildWeb(queryText, accepted);
        var history = new ChatHistory(prompt.SystemPrompt);
        history.AddUserMessage(prompt.UserPrompt);

        // Kaynaga dayali cevap: sicaklik 0, model kaynaklardan sapip alakasiz adimlar uydurmasin.
        var settings = new OpenAIPromptExecutionSettings { Temperature = 0 };
        var generationStopwatch = Stopwatch.StartNew();
        var response = await _chatService.GetChatMessageContentAsync(history, settings, _kernel, ct);
        generationStopwatch.Stop();
        var answer = (response.Content ?? string.Empty).Trim();

        if (answer.Length == 0 || answer.StartsWith(RagPromptBuilder.InsufficientMarker, StringComparison.OrdinalIgnoreCase))
        {
            return WebFallbackOutcome.GaveUp(webTrace, generationStopwatch.Elapsed);
        }

        var hallucinationStopwatch = Stopwatch.StartNew();
        var hallucinationCheck = await _hallucinationChecker.CheckWebAsync(answer, accepted, ct);
        hallucinationStopwatch.Stop();

        return new WebFallbackOutcome(answer, accepted, hallucinationCheck, generationStopwatch.Elapsed, hallucinationStopwatch.Elapsed, webTrace);
    }
}
