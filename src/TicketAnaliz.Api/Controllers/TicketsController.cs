using Microsoft.AspNetCore.Mvc;
using TicketAnaliz.Api.Contracts;
using TicketAnaliz.Api.Mapping;
using TicketAnaliz.Core.Rag;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketSearchService _ticketSearchService;
    private readonly IRagOrchestrationService _ragOrchestrationService;
    private readonly ISuggestionLogRepository _suggestionLogRepository;

    public TicketsController(
        ITicketSearchService ticketSearchService,
        IRagOrchestrationService ragOrchestrationService,
        ISuggestionLogRepository suggestionLogRepository)
    {
        _ticketSearchService = ticketSearchService;
        _ragOrchestrationService = ragOrchestrationService;
        _suggestionLogRepository = suggestionLogRepository;
    }

    // Sadece benzer ticket'lari bulur (semantic search), LLM cevabi uretmez.
    [HttpPost("search")]
    public async Task<ActionResult<TicketSearchResponse>> Search([FromBody] TicketSearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query alani bos olamaz.");
        }

        var results = await _ticketSearchService.SearchAsync(request.Query, request.TopK, ct);

        return Ok(new TicketSearchResponse { Results = results.Select(r => r.ToDto()).ToList() });
    }

    // Tam RAG akisi: gecmis kayitlar, gerekirse web arastirmasi, guven skoru, hallucination kontrolu.
    [HttpPost("suggest-solution")]
    public async Task<ActionResult<TicketSuggestionResponse>> SuggestSolution([FromBody] TicketSuggestionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query alani bos olamaz.");
        }

        var result = await _ragOrchestrationService.GenerateSuggestionAsync(request.Query, ct: ct);

        await _suggestionLogRepository.AddAsync(result.ToLog(request.Query), ct);

        return Ok(result.ToResponse());
    }
}
