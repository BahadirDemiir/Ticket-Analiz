using System.Text.Json;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.Core.Rag;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Evaluation;

public record EvaluationCaseResult(
    Guid TicketId,
    string Title,
    double Precision,
    double Recall,
    double ConfidencePercentage,
    bool Escalated,
    bool? HasHallucination,
    CorrectnessVerdict? Correctness,
    double TotalDurationMs);

public class EvaluationRunner
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketSearchService _ticketSearchService;
    private readonly IRagOrchestrationService _ragOrchestrationService;
    private readonly CorrectnessJudge _correctnessJudge;

    public EvaluationRunner(
        ITicketRepository ticketRepository,
        ITicketSearchService ticketSearchService,
        IRagOrchestrationService ragOrchestrationService,
        CorrectnessJudge correctnessJudge)
    {
        _ticketRepository = ticketRepository;
        _ticketSearchService = ticketSearchService;
        _ragOrchestrationService = ragOrchestrationService;
        _correctnessJudge = correctnessJudge;
    }

    public async Task RunAsync(int sampleCount)
    {
        Console.WriteLine("Tum ticketlar SQL'den okunuyor...");
        var allTickets = await _ticketRepository.GetAllAsync();
        Console.WriteLine($"Toplam {allTickets.Count} ticket bulundu.");

        var groupsByScenario = allTickets
            .Where(t => !string.IsNullOrEmpty(t.ScenarioKey))
            .GroupBy(t => t.ScenarioKey!)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToHashSet());

        // Sadece "held-out" (Qdrant'a hic yuklenmemis) ticketlari test sorgusu olarak kullaniyoruz.
        // Boylece retrieval sistemi daha once hic gormedigi bir metni bulmaya calisiyor,
        // kendi kendini bulup yapay bir yuksek skor almiyor.
        var heldOutTickets = allTickets.Where(t => !t.IsInVectorStore && t.ScenarioKey is not null).ToList();
        var sample = heldOutTickets.Take(sampleCount).ToList();
        Console.WriteLine($"{heldOutTickets.Count} held-out ticket var, {sample.Count} tanesi uzerinde degerlendirme yapilacak.");
        Console.WriteLine();

        var results = new List<EvaluationCaseResult>();

        foreach (var ticket in sample)
        {
            var queryText = $"{ticket.Title} {ticket.Description}";
            var groundTruth = groupsByScenario[ticket.ScenarioKey!].Where(id => id != ticket.Id).ToHashSet();

            // Retrieval degerlendirmesi. Ticket zaten Qdrant'ta olmadigi icin kendisiyle
            // karsilasma riski yok, yine de guvenlik icin filtreliyoruz.
            var searchResults = await _ticketSearchService.SearchAsync(queryText, topK: 5);
            var foundIds = searchResults.Select(r => r.Ticket.Id).Where(id => id != ticket.Id).ToList();

            var truePositives = foundIds.Count(id => groundTruth.Contains(id));
            var precision = foundIds.Count > 0 ? (double)truePositives / foundIds.Count : 0;
            var recall = groundTruth.Count > 0 ? (double)truePositives / groundTruth.Count : 0;

            // Tam RAG akisi: cevap, guven skoru, hallucination kontrolu, trace.
            var suggestion = await _ragOrchestrationService.GenerateSuggestionAsync(queryText);

            CorrectnessVerdict? correctness = null;
            if (!suggestion.Confidence.ShouldEscalate && !string.IsNullOrWhiteSpace(ticket.Resolution))
            {
                correctness = await _correctnessJudge.JudgeAsync(suggestion.Answer, ticket.Resolution);
            }

            var result = new EvaluationCaseResult(
                ticket.Id,
                ticket.Title,
                precision,
                recall,
                suggestion.Confidence.Percentage,
                suggestion.Confidence.ShouldEscalate,
                suggestion.HallucinationCheck?.HasUnsupportedClaims,
                correctness,
                suggestion.Trace.TotalDuration.TotalMilliseconds);

            results.Add(result);

            Console.WriteLine($"[{results.Count}/{sample.Count}] {ticket.Title}");
            Console.WriteLine($"    Precision: {precision:F2}  Recall: {recall:F2}  Guven: %{suggestion.Confidence.Percentage}  Correctness: {correctness?.ToString() ?? "-"}  Hallucination: {suggestion.HallucinationCheck?.HasUnsupportedClaims.ToString() ?? "-"}  Sure: {suggestion.Trace.TotalDuration.TotalMilliseconds:F0}ms");
        }

        PrintSummary(results);
        await WriteReportAsync(results);
    }

    private static void PrintSummary(List<EvaluationCaseResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=== OZET RAPOR ===");
        Console.WriteLine($"Toplam degerlendirilen ticket: {results.Count}");
        Console.WriteLine($"Ortalama Precision (retrieval dogrulugu): {results.Average(r => r.Precision):P1}");
        Console.WriteLine($"Ortalama Recall (retrieval kapsamligi):   {results.Average(r => r.Recall):P1}");

        var answered = results.Where(r => !r.Escalated).ToList();
        Console.WriteLine($"Cevaplanan sorgu sayisi: {answered.Count} / {results.Count} (kalani BT'ye yonlendirildi)");

        if (answered.Count > 0)
        {
            var correctnessScores = answered
                .Where(r => r.Correctness is not null)
                .Select(r => r.Correctness switch { CorrectnessVerdict.Evet => 1.0, CorrectnessVerdict.Kismen => 0.5, _ => 0.0 })
                .ToList();
            if (correctnessScores.Count > 0)
            {
                Console.WriteLine($"Dogru cozum onerme orani: {correctnessScores.Average():P1}");
            }

            var hallucinationCount = answered.Count(r => r.HasHallucination == true);
            Console.WriteLine($"Hallucination orani: {(double)hallucinationCount / answered.Count:P1} ({hallucinationCount}/{answered.Count})");
        }

        Console.WriteLine($"Ortalama cevap suresi: {results.Average(r => r.TotalDurationMs):F0} ms");
    }

    private static async Task WriteReportAsync(List<EvaluationCaseResult> results)
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "output");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"evaluation_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json);

        Console.WriteLine();
        Console.WriteLine($"Detayli rapor yazildi: {outputPath}");
    }
}
