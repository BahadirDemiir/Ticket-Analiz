using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TicketAnaliz.Core.Rag;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Infrastructure.Rag;

public class RagOrchestrationService : IRagOrchestrationService
{
    private readonly ITicketSearchService _ticketSearchService;
    private readonly RagPromptBuilder _promptBuilder;
    private readonly ConfidenceScoreCalculator _confidenceCalculator;
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public RagOrchestrationService(
        ITicketSearchService ticketSearchService,
        RagPromptBuilder promptBuilder,
        ConfidenceScoreCalculator confidenceCalculator,
        Kernel kernel)
    {
        _ticketSearchService = ticketSearchService;
        _promptBuilder = promptBuilder;
        _confidenceCalculator = confidenceCalculator;
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<RagSuggestionResult> GenerateSuggestionAsync(string queryText, CancellationToken ct = default)
    {
        // Semantic search ile alakali gecmis ticket'lari bul.
        var sources = await _ticketSearchService.SearchAsync(queryText, topK: 5, ct: ct);

        // Guven skorunu, LLM'i cagirmadan ONCE, sadece bulunan kaynaklara bakarak hesapla.
        var confidence = _confidenceCalculator.Calculate(sources);

        // 3. ADIM: Guven yeterince dusukse LLM'e hic gitme
        if (confidence.ShouldEscalate)
        {
            return new RagSuggestionResult(confidence.Message, sources, confidence);
        }

        // Bulunanlari, rastgele boundary ile guvenli bir prompt'a yerlestir, prompt injection korumasý
        var prompt = _promptBuilder.Build(queryText, sources);

        // LLM'e gonder, cevabi al.
        var history = new ChatHistory(prompt.SystemPrompt);
        history.AddUserMessage(prompt.UserPrompt);

        var response = await _chatService.GetChatMessageContentAsync(history, kernel: _kernel, cancellationToken: ct);
        var answer = response.Content ?? string.Empty;

        return new RagSuggestionResult(answer, sources, confidence);
    }
}
