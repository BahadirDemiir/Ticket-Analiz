using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TicketAnaliz.Infrastructure.Search;

// Kullanicinin (cogunlukla Turkce, gunluk dille yazilmis) sikayetini web aramasina uygun kisa bir
// Ingilizce teknik sorguya cevirir. Adim 2 testinde ham Turkce cumle Tavily'de cop sonuc verdi.
public class WebQueryRewriter
{
    private const string NoQuerySentinel = "NONE";

    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public WebQueryRewriter(Kernel kernel)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    // Uygun bir teknik arama sorgusu cikarilamazsa (sikayet teknik bir sorun degilse) null doner.
    public async Task<string?> RewriteAsync(string userComplaint, CancellationToken ct = default)
    {
        var prompt = $"""
            Aşağıda bir çalışanın destek talebi olarak yazdığı şikayet var. Görevin, bu şikayeti bir
            internet arama motorunda (Google benzeri) aratılacak KISA bir İNGİLİZCE teknik sorguya çevirmek.

            Kurallar:
            - En fazla 12 kelime. Sadece anahtar kelimeler: ürün/sistem adı, hata türü, varsa hata kodu,
              işlem kodu veya bileşen adı. Tam cümle kurma.
            - Kelime kelime çeviri yapma: şikayetteki ürün/sistemin uzmanlarının bu sorunu aramak için
              gerçekten kullandığı teknik terimleri seç (örneğin bir sistemin kendi hata mesajı veya
              standart teşhis aracı adı). Yanlış bağlama kayan genel kelimelerden kaçın (örneğin
              yetki sorununu ürünün kendi yetkilendirme terimleriyle anlat, genel web/HTTP hata
              terimleriyle değil).
            - Şikayette adı geçmeyen bir ürün ekleme ve hata kodu UYDURMA.
            - Şikayet bir yazılım/sistem/altyapı sorunu DEĞİLSE (teknik olmayan bir şikayet, şahsi bir konu,
              sohbet vb.), arama yapılmamalı: bu durumda sadece {NoQuerySentinel} yaz.
            - Cevabın sadece sorgu metni (veya {NoQuerySentinel}) olsun, başka hiçbir şey yazma.

            Şikayet:
            {userComplaint}
            """;

        // Ayni sikayet her seferinde ayni sorguyu uretsin diye sicaklik 0.
        var settings = new OpenAIPromptExecutionSettings { Temperature = 0 };
        var response = await _chatService.GetChatMessageContentAsync(prompt, settings, _kernel, ct);
        var query = (response.Content ?? string.Empty).Trim().Trim('"', '\'', '`').Trim();

        if (query.Length == 0 || query.Equals(NoQuerySentinel, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return query;
    }
}
