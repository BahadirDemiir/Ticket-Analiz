using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace TicketAnaliz.SeedGenerator.Services;

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
            Örneğin "ekran donuyor" ile "ekran boş kalıyor" farklı kök nedenlerdir, alakalı sayma.

            Adaylar:
            {candidatesText}

            Sadece şu JSON formatında cevap ver, başka hiçbir metin yazma:
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

    private class RerankResult
    {
        public List<int> RelevantIndices { get; set; } = new();
    }
}
