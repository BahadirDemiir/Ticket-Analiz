using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TicketAnaliz.Evaluation;

public enum WebRelevanceVerdict { Alakali, Kismen, Alakasiz }

public record WebRelevanceJudgement(WebRelevanceVerdict Verdict, string Reason);

// Web fallback cevabinin, onceden yazilmis "beklenen konu" notuna gore alakali olup olmadigina karar verir.
// Beklenen konu elle yazildigi icin yargic kendi kafasindan degil, sabit bir referansa gore karar veriyor.
public class WebRelevanceJudge
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public WebRelevanceJudge(Kernel kernel)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<WebRelevanceJudgement> JudgeAsync(string userQuery, string expectedTopic, string answer, CancellationToken ct = default)
    {
        const string jsonFormatExample = """{"reason": "1-2 cümlelik gerekçe", "verdict": "Alakali" veya "Kismen" veya "Alakasiz"}""";

        var prompt = $"""
            Bir destek asistanı, kullanıcının sorununa internet kaynaklarından bir çözüm önerisi üretti.
            Görevin, bu önerinin kullanıcının SORUNUYLA gerçekten ilgili olup olmadığını değerlendirmek.

            KULLANICININ SORUNU:
            {userQuery}

            DOĞRU BİR CEVAP NE HAKKINDA OLMALI (referans):
            {expectedTopic}

            ASİSTANIN ÜRETTİĞİ CEVAP:
            {answer}

            Değerlendirme:
            - "Alakali": öneri referanstaki sorun türünü ele alıyor ve adımları kullanıcıya gerçekten yardımcı olabilir.
            - "Kismen": öneri doğru konuya yakın ama sorunun asıl kök nedenini/durumunu kaçırıyor veya çoğunlukla genel geçer.
            - "Alakasiz": öneri farklı bir sorun türünü anlatıyor (referansın "yanlıştır" dediği türler dahil) ve kullanıcıyı yanlış yöne sürükler.
            Önerideki adımların teknik olarak her ayrıntısının doğru olup olmadığına değil, DOĞRU SORUNU ele alıp almadığına bak.

            Önce kısa gerekçeni yaz, sonra kararını ver. Sadece şu JSON formatında cevap ver:
            {jsonFormatExample}
            """;

        var settings = new OpenAIPromptExecutionSettings { Temperature = 0 };
        var response = await _chatService.GetChatMessageContentAsync(prompt, settings, _kernel, ct);
        var json = ExtractJson(response.Content ?? "{}");

        try
        {
            var dto = JsonSerializer.Deserialize<JudgementDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var verdict = dto?.Verdict?.Trim().ToLowerInvariant() switch
            {
                "alakali" => WebRelevanceVerdict.Alakali,
                "kismen" => WebRelevanceVerdict.Kismen,
                _ => WebRelevanceVerdict.Alakasiz
            };
            return new WebRelevanceJudgement(verdict, dto?.Reason ?? "");
        }
        catch (JsonException)
        {
            return new WebRelevanceJudgement(WebRelevanceVerdict.Alakasiz, "Yargıç cevabı ayrıştırılamadı.");
        }
    }

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        var open = trimmed.IndexOf('{');
        var close = trimmed.LastIndexOf('}');
        return open >= 0 && close > open ? trimmed[open..(close + 1)] : trimmed;
    }

    private class JudgementDto
    {
        public string? Reason { get; set; }
        public string? Verdict { get; set; }
    }
}
