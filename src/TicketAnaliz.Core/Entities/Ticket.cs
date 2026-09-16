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
}
