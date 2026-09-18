using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace TicketAnaliz.Infrastructure.Search;

public record RerankCandidate(int Index, string Title, string Description);

public class RerankingService
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public RerankingService(Kernel kernel)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<List<int>> GetRelevantIndicesAsync(string query, IReadOnlyList<RerankCandidate> candidates)
    {
        var candidatesText = string.Join("\n\n", candidates.Select(c =>
            $"[{c.Index}] Başlık: {c.Title}\n    Açıklama: {c.Description}"));
        const string jsonFormatExample = """{"relevantIndices": [alakalı bulduğun adayların index numaraları]}""";

        var prompt = $"""
            Kullanıcının sorduğu soru: "{query}"

            Aşağıda semantic search ile bulunan aday ticket'lar var. Görevin, bunlardan HANGİLERİNİN
            kullanıcının sorduğu sorunla GERÇEKTEN aynı veya çok yakın konuda olduğunu belirlemek.
            Sadece yüzeysel kelime benzerliğine değil, gerçek konu/kök neden benzerliğine bak.

            Önce, ÖNCE HİÇBİR ŞEY VARSAYMADAN her aday için ayrı ayrı şunu değerlendir: sorgu ile
            aday, aynı somut olay/sorun türünü mü anlatıyor, yoksa sadece bazı kelimeler rastlantısal
            olarak mı örtüşüyor? Bunu bir-iki cümlelik kısa bir gerekçeyle her aday için yaz
            (örn. "[0] Alakalı degil - sorgu X hakkinda, bu aday Y hakkinda, sadece '...' kelimesi
            ortak"). Örneğin "ekran donuyor" ile "ekran boş kalıyor" farklı kök nedenlerdir, alakalı
            sayma. Sorgu tamamen farklı bir konudaysa (teknik bir arıza değil de bambaşka bir şeyse),
            hiçbir adayı zorla alakalı gösterme - boş liste dönmek tamamen kabul edilebilir bir sonuç.

            Adaylar:
            {candidatesText}

            Gerekçelerini yazdıktan SONRA, en son satırda ve SADECE o satırda şu JSON formatında
            nihai kararını ver (gerekçe metninin JSON'un içine karışmasın):
            {jsonFormatExample}
            """;

        var response = await _chatService.GetChatMessageContentAsync(prompt, kernel: _kernel);
        var rawContent = response.Content ?? "{}";
        var json = ExtractJson(rawContent);

        try
        {
            var result = JsonSerializer.Deserialize<RerankResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return result?.RelevantIndices ?? candidates.Select(c => c.Index).ToList();
        }
        catch (JsonException)
        {
            // LLM beklenmedik bir format donduyse, guvenli tarafta kal: hicbirini eleme.
            return candidates.Select(c => c.Index).ToList();
        }
    }

    private static string ExtractJson(string content)
    {
        // Artik cevap once gerekce metni, en sonda JSON iceriyor - "son { ... son }" arasini al.
        var trimmed = content.Trim();
        var lastOpenBrace = trimmed.LastIndexOf('{');
        var lastCloseBrace = trimmed.LastIndexOf('}');

        if (lastOpenBrace >= 0 && lastCloseBrace > lastOpenBrace)
        {
            return trimmed[lastOpenBrace..(lastCloseBrace + 1)];
        }

        return trimmed;
    }

    private class RerankResult
    {
        public List<int> RelevantIndices { get; set; } = new();
    }
}
