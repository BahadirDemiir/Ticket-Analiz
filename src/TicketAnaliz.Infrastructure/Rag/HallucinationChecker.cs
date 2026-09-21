using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TicketAnaliz.Core.Rag;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Infrastructure.Rag;

public class HallucinationChecker : IHallucinationChecker
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public HallucinationChecker(Kernel kernel)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    // cevap ticketlardan üretildiyse kaynak olarak tickettaki verileri kullan
    public Task<HallucinationCheckResult> CheckAsync(string answer, IReadOnlyList<TicketSearchResult> sources, CancellationToken ct = default)
    {
        var sourcesText = string.Join("\n\n", sources.Select((s, i) =>
            $"[Kaynak {i + 1}]\nBaşlık: {s.Ticket.Title}\nAçıklama: {s.Ticket.Description}\nÇözüm: {s.Ticket.Resolution}"));

        return CheckAgainstTextAsync(answer, sourcesText, ct);
    }

    // cevap webden üretildiyse kaynak olarak webdeki verileri kullan

    public Task<HallucinationCheckResult> CheckWebAsync(string answer, IReadOnlyList<WebSearchResult> sources, CancellationToken ct = default)
    {
        var sourcesText = string.Join("\n\n", sources.Select((s, i) =>
            $"[Kaynak {i + 1}]\nBaşlık: {s.Title}\nİçerik: {s.Content}"));

        return CheckAgainstTextAsync(answer, sourcesText, ct);
    }

    private async Task<HallucinationCheckResult> CheckAgainstTextAsync(string answer, string sourcesText, CancellationToken ct)
    {
        const string jsonFormatExample = """{"hasUnsupportedClaims": true veya false, "explanation": "kisa aciklama"}""";

        var prompt = $"""
            Aşağıda bir LLM'in ürettiği çözüm önerisi ve bu önerinin dayandığı iddia edilen
            kaynak ticket'lar var. Görevin, önerinin kaynaklarda YER ALMAYAN hiçbir teknik
            detay, komut veya adım içerip içermediğini kontrol etmek. Kaynaklardaki bilgilerin
            farklı kelimelerle özetlenmesi UYDURMA sayılmaz, sadece kaynaklarda hiç geçmeyen
            yeni bir bilginin eklenmesi UYDURMA sayılır.

            CEVAP:
            {answer}

            KAYNAKLAR:
            {sourcesText}

            Sadece şu JSON formatında cevap ver, başka hiçbir metin yazma:
            {jsonFormatExample}
            """;

        var response = await _chatService.GetChatMessageContentAsync(prompt, kernel: _kernel, cancellationToken: ct);
        var rawContent = response.Content ?? "{}";
        var json = ExtractJson(rawContent);

        try
        {
            var result = JsonSerializer.Deserialize<HallucinationCheckDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new HallucinationCheckResult(
                result?.HasUnsupportedClaims ?? false,
                result?.Explanation ?? "Kontrol sonucu ayrıştırılamadı.");
        }
        catch (JsonException)
        {
            return new HallucinationCheckResult(true, "Kontrol sonucu ayrıştırılamadı, temkinli olarak şüpheli işaretlendi.");
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

    private class HallucinationCheckDto
    {
        public bool HasUnsupportedClaims { get; set; }
        public string Explanation { get; set; } = default!;
    }
}
