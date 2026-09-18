using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace TicketAnaliz.Evaluation;

public enum CorrectnessVerdict { Evet, Kismen, Hayir }

public class CorrectnessJudge
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public CorrectnessJudge(Kernel kernel)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<CorrectnessVerdict> JudgeAsync(string generatedAnswer, string referenceResolution, CancellationToken ct = default)
    {
        const string jsonFormatExample = """{"verdict": "Evet" veya "Kismen" veya "Hayir"}""";

        var prompt = $"""
            Aşağıda bir LLM'in ürettiği çözüm önerisi ve bu ticket için gerçekte uygulanmış
            olan referans çözüm var. Bu ikisi AYNI TEMEL ÇÖZÜMÜ mü öneriyor? Farklı kelimelerle
            anlatılmış olması önemli değil, kök çözüm aynıysa "Evet" de. Kısmen örtüşüyorsa
            "Kismen", tamamen farklı bir çözümse "Hayir" de.

            ÜRETİLEN CEVAP:
            {generatedAnswer}

            REFERANS ÇÖZÜM:
            {referenceResolution}

            Sadece şu JSON formatında cevap ver, başka hiçbir metin yazma:
            {jsonFormatExample}
            """;

        var response = await _chatService.GetChatMessageContentAsync(prompt, kernel: _kernel, cancellationToken: ct);
        var json = ExtractJson(response.Content ?? "{}");

        try
        {
            var result = JsonSerializer.Deserialize<VerdictDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.Verdict?.Trim().ToLowerInvariant() switch
            {
                "evet" => CorrectnessVerdict.Evet,
                "kismen" => CorrectnessVerdict.Kismen,
                _ => CorrectnessVerdict.Hayir
            };
        }
        catch (JsonException)
        {
            return CorrectnessVerdict.Hayir;
        }
    }

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```");
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }
        return trimmed;
    }

    private class VerdictDto
    {
        public string? Verdict { get; set; }
    }
}
