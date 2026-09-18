using Microsoft.AspNetCore.Mvc;
using TicketAnaliz.Api.Contracts;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketSearchService _ticketSearchService;

    public TicketsController(ITicketSearchService ticketSearchService)
    {
        _ticketSearchService = ticketSearchService;
    }

    [HttpPost("search")] // orkestra þefi olan TicketSearchService'ye ticket bilgilerini gönderip aramayý tetikleyecek endpoint
    public async Task<ActionResult<TicketSearchResponse>> Search([FromBody] TicketSearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query alani bos olamaz.");
        }

        var results = await _ticketSearchService.SearchAsync(request.Query, request.TopK, ct);

        var response = new TicketSearchResponse
        {
            Results = results.Select(r => new TicketSearchResultDto
            {
                Id = r.Ticket.Id,
                Title = r.Ticket.Title,
                Description = r.Ticket.Description,
                Category = r.Ticket.Category,
                Department = r.Ticket.Department.ToString(),
                Status = r.Ticket.Status.ToString(),
                Resolution = r.Ticket.Resolution,
                Priority = r.Ticket.Priority,
                Score = r.Score
            }).ToList()
        };

        return Ok(response);
    }
}
