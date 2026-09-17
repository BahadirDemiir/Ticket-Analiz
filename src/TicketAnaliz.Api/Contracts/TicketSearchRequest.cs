namespace TicketAnaliz.Api.Contracts;

public class TicketSearchRequest
{
    public string Query { get; set; } = default!;
    public int TopK { get; set; } = 5;
}
