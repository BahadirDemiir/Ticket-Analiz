namespace TicketAnaliz.Api.Contracts;

public class TicketSearchResponse
{
    public List<TicketSearchResultDto> Results { get; set; } = new();
}

public class TicketSearchResultDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Department { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Resolution { get; set; }
    public string? Priority { get; set; }
    public float Score { get; set; }
}
