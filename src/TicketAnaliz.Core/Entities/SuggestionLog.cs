using TicketAnaliz.Core.Rag;

namespace TicketAnaliz.Core.Entities;

// Her POST /api/tickets/suggest-solution cagrisinin kaydi - admin/debug
// sayfasinda gecmis sorgulari listeleyebilmek icin.
public class SuggestionLog
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Query { get; set; } = default!;

    // Sorguyu kimin sordugu. Giris sistemi gelmeden onceki kayitlarda bos.
    public string? UserName { get; set; }
    public string Answer { get; set; } = default!;

    public double ConfidencePercentage { get; set; }
    public bool ShouldEscalate { get; set; }
    public double ConfidenceAverageSimilarity { get; set; }
    public double ConfidenceResolvedRatio { get; set; }
    public double ConfidenceSourceCountFactor { get; set; }

    public bool? HasHallucination { get; set; }
    public string? HallucinationExplanation { get; set; }

    // Kaynak ticket'larin (id, baslik, skor) listesi JSON olarak saklanir -
    // ayri bir cocuk tablo acmaya gerek yok, sadece gecmis goruntuleme icin.
    public string SourcesJson { get; set; } = "[]";

    // Cevabin nereden geldigi. ShouldEscalate artik "sonuc olarak BT'ye yonlendirildi mi" demek
    // (Source == Escalated); ConfidencePercentage ise hep gecmis kayitlarin skorudur.
    public AnswerSource AnswerSource { get; set; } = AnswerSource.HistoricalTickets;

    // Web fallback devreye girdiyse: kullanilan web kaynaklari (baslik, url, skor) JSON olarak,
    // ve web aramasinin ara adimlari. Fallback denenmediyse hepsi bos.
    public string WebSourcesJson { get; set; } = "[]";
    public string? WebSearchQuery { get; set; }
    public int? WebSearchResultCount { get; set; }
    public int? WebSearchAcceptedCount { get; set; }
    public double? WebSearchDurationMs { get; set; }

    public double SearchDurationMs { get; set; }
    public double? GenerationDurationMs { get; set; }
    public double? HallucinationCheckDurationMs { get; set; }
    public double TotalDurationMs { get; set; }
}
