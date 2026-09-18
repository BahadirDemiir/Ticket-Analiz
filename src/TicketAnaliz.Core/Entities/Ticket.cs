namespace TicketAnaliz.Core.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!;
    public Department Department { get; set; }
    public string? Resolution { get; set; }
    public TicketStatus Status { get; set; }
    public string? Priority { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public bool IsSynthetic { get; set; } = true;

    // Sentetik veri uretiminde ayni kok soruna ait varyasyonlari gruplamak icin kullanilir
    // (orn. "rfc_1"). Evaluation'da hangi ticket'larin ayni senaryodan geldigini bulmak icin.
    public string? ScenarioKey { get; set; }

    // Bu ticket embed edilip Qdrant'a yuklendi mi? False ise "held-out" (tutulan) bir test
    // sorgusu demektir - retrieval sistemi bunu hic gormemis olmali, evaluation'da gercek bir
    // "daha once gorulmemis soru" testi yapabilmek icin bilerek Qdrant'a yuklenmez.
    public bool IsInVectorStore { get; set; }
}
