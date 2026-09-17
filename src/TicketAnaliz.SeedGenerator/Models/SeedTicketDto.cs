namespace TicketAnaliz.SeedGenerator.Models;

public class SeedTicketDto
{
    public string ScenarioKey { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Department { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? Resolution { get; set; }
    public string Status { get; set; } = default!;
    public string? Priority { get; set; }
    public int CreatedDaysAgo { get; set; }
    public int? ResolvedDaysAgo { get; set; }
}

public class SeedTicketFile
{
    public List<SeedTicketDto> Tickets { get; set; } = new();
}
