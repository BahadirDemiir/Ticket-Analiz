using System.Diagnostics;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TicketAnaliz.Core.Rag;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Infrastructure.Rag;

public class RagOrchestrationService : IRagOrchestrationService
{
    private readonly ITicketSearchService _ticketSearchService;
    private readonly IWebFallbackService _webFallbackService;
    private readonly RagPromptBuilder _promptBuilder;
    private readonly ConfidenceScoreCalculator _confidenceCalculator;
    private readonly IHallucinationChecker _hallucinationChecker;
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public RagOrchestrationService(
        ITicketSearchService ticketSearchService,
        IWebFallbackService webFallbackService,
        RagPromptBuilder promptBuilder,
        ConfidenceScoreCalculator confidenceCalculator,
        IHallucinationChecker hallucinationChecker,
        Kernel kernel)
    {
        _ticketSearchService = ticketSearchService;
        _webFallbackService = webFallbackService;
        _promptBuilder = promptBuilder;
        _confidenceCalculator = confidenceCalculator;
        _hallucinationChecker = hallucinationChecker;
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<RagSuggestionResult> GenerateSuggestionAsync(string queryText, bool allowWebFallback = true, CancellationToken ct = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        // Semantic search ile alakali gecmis ticket'lari bul.
        var searchStopwatch = Stopwatch.StartNew();
        var sources = await _ticketSearchService.SearchAsync(queryText, topK: 5, ct: ct);
        searchStopwatch.Stop();

        // Guven skorunu, LLM'i cagirmadan ONCE, sadece bulunan kaynaklara bakarak hesapla.
        var confidence = _confidenceCalculator.Calculate(sources);

        // Guven yeterince dusukse gecmis kayitlarla LLM'e hic gitme
        if (confidence.ShouldEscalate)
        {
            WebFallbackOutcome? web = null;
            if (allowWebFallback)
            {
                web = await _webFallbackService.TryAsync(queryText, ct);
            }

            totalStopwatch.Stop();

            if (web?.Answer is not null)
            {
                var webTrace = new RagTrace(searchStopwatch.Elapsed, web.GenerationDuration, web.HallucinationCheckDuration, totalStopwatch.Elapsed, web.Trace);
                return new RagSuggestionResult(web.Answer, sources, confidence, web.HallucinationCheck, webTrace, AnswerSource.WebSearch, web.Sources);
            }

            var escalatedTrace = new RagTrace(searchStopwatch.Elapsed, null, null, totalStopwatch.Elapsed, web?.Trace);
            return new RagSuggestionResult(confidence.Message, sources, confidence, HallucinationCheck: null, escalatedTrace, AnswerSource.Escalated);
        }

        // Bulunanlari, rastgele boundary ile guvenli bir prompt'a yerlestir, prompt injection korumasi
        var prompt = _promptBuilder.Build(queryText, sources);

        // LLM'e gonder, cevabi al.
        var history = new ChatHistory(prompt.SystemPrompt);
        history.AddUserMessage(prompt.UserPrompt);

        var generationStopwatch = Stopwatch.StartNew();
        var response = await _chatService.GetChatMessageContentAsync(history, kernel: _kernel, cancellationToken: ct);
        generationStopwatch.Stop();
        var answer = response.Content ?? string.Empty;

        // Cevap uretildikten sonra, gercekten sadece kaynaklara mi dayandigini kontrol et.
        var hallucinationStopwatch = Stopwatch.StartNew();
        var hallucinationCheck = await _hallucinationChecker.CheckAsync(answer, sources, ct);
        hallucinationStopwatch.Stop();

        totalStopwatch.Stop();
        var trace = new RagTrace(searchStopwatch.Elapsed, generationStopwatch.Elapsed, hallucinationStopwatch.Elapsed, totalStopwatch.Elapsed);

        return new RagSuggestionResult(answer, sources, confidence, hallucinationCheck, trace);
    }
}
