using System.Text.Json;
using TicketAnaliz.Core.Rag;

namespace TicketAnaliz.Evaluation;

public record WebTestCase(string Id, string Kind, string Query, string ExpectedTopic);

public enum WebRunOutcome { AnsweredRelevant, AnsweredPartial, AnsweredIrrelevant, GaveUp }

public record WebRunResult(
    string CaseId,
    string Kind,
    int Run,
    WebRunOutcome Outcome,
    string? RewrittenQuery,
    int WebResultCount,
    int AcceptedCount,
    List<string> SourceTitles,
    bool? HasHallucination,
    string? Verdict,
    string? JudgeReason,
    string? Answer,
    double DurationMs);

// Web fallback yolunu, gecmis kayit aramasindan BAGIMSIZ olarak (dogrudan IWebFallbackService uzerinden)
// olcer. Ayni sorgu birkac kez calistirilir, cunku LLM sorgu yeniden yazimi her seferinde ayni cikmiyor.
public class WebFallbackEvaluator
{
    private readonly IWebFallbackService _webFallbackService;
    private readonly WebRelevanceJudge _judge;

    public WebFallbackEvaluator(IWebFallbackService webFallbackService, WebRelevanceJudge judge)
    {
        _webFallbackService = webFallbackService;
        _judge = judge;
    }

    public async Task RunAsync(int repeats, string label)
    {
        var casesPath = Path.Combine(AppContext.BaseDirectory, "Data", "web_fallback_testset.json");
        var cases = JsonSerializer.Deserialize<List<WebTestCase>>(
            await File.ReadAllTextAsync(casesPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Console.WriteLine($"Web fallback olcumu [{label}]: {cases.Count} sorgu x {repeats} tekrar = {cases.Count * repeats} kosu");
        Console.WriteLine();

        var results = new List<WebRunResult>();

        foreach (var c in cases)
        {
            for (var run = 1; run <= repeats; run++)
            {
                var outcome = await _webFallbackService.TryAsync(c.Query);

                WebRelevanceJudgement? judgement = null;
                WebRunOutcome runOutcome;

                if (outcome.Answer is null)
                {
                    runOutcome = WebRunOutcome.GaveUp;
                }
                else
                {
                    judgement = await _judge.JudgeAsync(c.Query, c.ExpectedTopic, outcome.Answer);
                    runOutcome = judgement.Verdict switch
                    {
                        WebRelevanceVerdict.Alakali => WebRunOutcome.AnsweredRelevant,
                        WebRelevanceVerdict.Kismen => WebRunOutcome.AnsweredPartial,
                        _ => WebRunOutcome.AnsweredIrrelevant
                    };
                }

                var result = new WebRunResult(
                    c.Id, c.Kind, run, runOutcome,
                    outcome.Trace.RewrittenQuery,
                    outcome.Trace.ResultCount,
                    outcome.Trace.AcceptedCount,
                    outcome.Sources.Select(s => s.Title).ToList(),
                    outcome.HallucinationCheck?.HasUnsupportedClaims,
                    judgement?.Verdict.ToString(),
                    judgement?.Reason,
                    outcome.Answer,
                    outcome.Trace.Duration.TotalMilliseconds
                        + (outcome.GenerationDuration?.TotalMilliseconds ?? 0)
                        + (outcome.HallucinationCheckDuration?.TotalMilliseconds ?? 0));
                results.Add(result);

                Console.WriteLine($"[{c.Id} #{run}] {runOutcome,-20} sorgu: {outcome.Trace.RewrittenQuery ?? "(uretilmedi)"}  ({outcome.Trace.AcceptedCount}/{outcome.Trace.ResultCount} kabul)");
            }
        }

        PrintSummary(results, cases, repeats);
        await WriteReportAsync(results, label);
    }

    private static void PrintSummary(List<WebRunResult> results, List<WebTestCase> cases, int repeats)
    {
        var technical = results.Where(r => r.Kind == "technical").ToList();
        var nontechnical = results.Where(r => r.Kind == "nontechnical").ToList();

        Console.WriteLine();
        Console.WriteLine("=== OZET: TEKNIK SORGULAR (web'den cevap beklenen) ===");
        Console.WriteLine($"Kosu sayisi: {technical.Count}");
        Console.WriteLine($"  Alakali cevap:           {Rate(technical, WebRunOutcome.AnsweredRelevant)}");
        Console.WriteLine($"  Kismen alakali cevap:    {Rate(technical, WebRunOutcome.AnsweredPartial)}");
        Console.WriteLine($"  ALAKASIZ cevap (yanlis): {Rate(technical, WebRunOutcome.AnsweredIrrelevant)}");
        Console.WriteLine($"  Cevap vermedi (BT'ye):   {Rate(technical, WebRunOutcome.GaveUp)}");

        var answered = technical.Where(r => r.Outcome != WebRunOutcome.GaveUp).ToList();
        if (answered.Count > 0)
        {
            var halluc = answered.Count(r => r.HasHallucination == true);
            Console.WriteLine($"  Cevaplananlarda hallucination isareti: {halluc}/{answered.Count}");
        }

        Console.WriteLine();
        Console.WriteLine("=== OZET: TEKNIK OLMAYAN SORGULAR (cevap VERILMEMESI beklenen) ===");
        Console.WriteLine($"Kosu sayisi: {nontechnical.Count}");
        Console.WriteLine($"  Dogru sekilde reddedildi: {Rate(nontechnical, WebRunOutcome.GaveUp)}");
        var wrongly = nontechnical.Count(r => r.Outcome != WebRunOutcome.GaveUp);
        Console.WriteLine($"  Yanlislikla cevaplandi:   {wrongly}/{nontechnical.Count}");

        Console.WriteLine();
        Console.WriteLine($"Ortalama sure: {results.Average(r => r.DurationMs):F0} ms");
        Console.WriteLine($"Web aramasi yapilan kosu: {results.Count(r => r.RewrittenQuery is not null)} (Tavily cagrisi)");

        Console.WriteLine();
        Console.WriteLine($"=== SORGU BAZINDA ({repeats} tekrar; A=alakali K=kismen X=alakasiz G=cevap vermedi) ===");
        foreach (var c in cases)
        {
            var runs = results.Where(r => r.CaseId == c.Id).OrderBy(r => r.Run)
                .Select(r => r.Outcome switch
                {
                    WebRunOutcome.AnsweredRelevant => "A",
                    WebRunOutcome.AnsweredPartial => "K",
                    WebRunOutcome.AnsweredIrrelevant => "X",
                    _ => "G"
                });
            Console.WriteLine($"  {c.Id,-16} {string.Join(" ", runs)}");
        }
    }

    private static string Rate(List<WebRunResult> results, WebRunOutcome outcome)
    {
        var count = results.Count(r => r.Outcome == outcome);
        return $"{count}/{results.Count} ({(results.Count > 0 ? (double)count / results.Count : 0):P0})";
    }

    private static async Task WriteReportAsync(List<WebRunResult> results, string label)
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "output");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"webfallback_{label}_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(outputPath, json);

        Console.WriteLine();
        Console.WriteLine($"Detayli rapor yazildi: {outputPath}");
    }
}
