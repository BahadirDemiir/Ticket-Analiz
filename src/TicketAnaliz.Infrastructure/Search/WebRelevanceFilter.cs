using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TicketAnaliz.Infrastructure.Search;

// Web sonuclari icin alaka suzgeci. RerankingService gecmis ticket'lar icin yazildi ve "ayni kok neden mi?"
// diye katı soruyor; internet yazilari ise bizim ticket'imizla birebir ayni olayi anlatmaz, bu yuzden
// (olcumde) isabetli sonuclari da eliyordu. Burada olcut: "ayni urun/bilesen ve ayni belirti turu, yardimci olabilir mi?"
public class WebRelevanceFilter
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public WebRelevanceFilter(Kernel kernel)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<List<int>> GetRelevantIndicesAsync(string query, IReadOnlyList<RerankCandidate> candidates, CancellationToken ct = default)
    {
        var candidatesText = string.Join("\n\n", candidates.Select(c =>
            $"[{c.Index}] Başlık: {c.Title}\n    İçerik: {c.Description}"));
        const string jsonFormatExample = """{"relevantIndices": [yardımcı olabilecek kaynakların index numaraları]}""";

        var prompt = $"""
            Bir çalışan destek talebi olarak şu sorunu yazdı: "{query}"

            Aşağıda internet aramasıyla bulunan kaynaklar var. Görevin, bunlardan HANGİLERİNİN bu
            çalışanın sorununu çözmeye gerçekten yardımcı olabileceğini belirlemek.

            Bir kaynak şu koşulları sağlıyorsa yardımcı olabilir sayılır:
            - Aynı ürün/sistem/bileşenle ilgilidir (kullanıcı hangi ürünü söylediyse o; söylemediyse
              sorunun doğal olarak ait olduğu ürün).
            - Aynı TÜR belirtiyi/hatayı ele alır. Kaynağın kullanıcıyla birebir aynı olayı anlatması
              gerekmez; aynı sorun sınıfı için sorun giderme adımları veriyorsa yeterlidir.

            Şu durumlarda yardımcı OLMAZ:
            - Farklı bir ürün veya bileşen hakkındadır.
            - Aynı kelimeyi içerir ama farklı bir sorun türünü anlatır (kelime benzerliği yeterli değildir;
              aynı sözcük farklı teknik bağlamlarda tamamen farklı anlamlara gelebilir).
            - Sadece haber, duyuru, reklam, ürün tanıtımı ya da sorunla ilgisiz genel bilgidir.

            Önce her kaynak için bir-iki cümlelik gerekçe yaz: hangi ürün/bileşen, hangi belirti türü ve
            bunun kullanıcının sorunuyla uyuşup uyuşmadığı. Hiçbiri uygun değilse boş liste dönmek kabul edilebilir.

            Kaynaklar:
            {candidatesText}

            Gerekçelerini yazdıktan SONRA, en son satırda ve SADECE o satırda şu JSON formatında
            nihai kararını ver:
            {jsonFormatExample}
            """;

        var settings = new OpenAIPromptExecutionSettings { Temperature = 0 };
        var response = await _chatService.GetChatMessageContentAsync(prompt, settings, _kernel, ct);
        var json = ExtractJson(response.Content ?? "{}");

        try
        {
            var result = JsonSerializer.Deserialize<FilterResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.RelevantIndices ?? new List<int>();
        }
        catch (JsonException)
        {
            // Web icerigi dogrulanmamis: karar okunamazsa hicbirini kabul etme, BT'ye yonlendirme kalsin.
            return new List<int>();
        }
    }

    // Cevap once gerekce metni, en sonda JSON iceriyor: "son { ... son }" arasini al.
    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        var open = trimmed.LastIndexOf('{');
        var close = trimmed.LastIndexOf('}');
        return open >= 0 && close > open ? trimmed[open..(close + 1)] : trimmed;
    }

    private class FilterResult
    {
        public List<int> RelevantIndices { get; set; } = new();
    }
}
