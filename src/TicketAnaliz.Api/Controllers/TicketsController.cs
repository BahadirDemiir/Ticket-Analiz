using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TicketAnaliz.Api.Contracts;
using TicketAnaliz.Core.Entities;
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

    [HttpPost("search")] // orkestra �efi olan TicketSearchService'ye ticket bilgilerini g�nderip aramay� tetikleyecek endpoint
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

    [HttpPost("suggest-solution")]
    public async Task<ActionResult<TicketSuggestionResponse>> SuggestSolution([FromBody] TicketSuggestionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query alani bos olamaz.");
        }

        var result = await _ragOrchestrationService.GenerateSuggestionAsync(request.Query, ct);

        var response = new TicketSuggestionResponse
        {
            Answer = result.Answer,
            ConfidencePercentage = result.Confidence.Percentage,
            ShouldEscalate = result.Confidence.ShouldEscalate,
            Sources = result.Sources.Select(r => new TicketSearchResultDto
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
            }).ToList(),
            HallucinationCheck = result.HallucinationCheck is null
                ? null
                : new HallucinationCheckDto
                {
                    HasUnsupportedClaims = result.HallucinationCheck.HasUnsupportedClaims,
                    Explanation = result.HallucinationCheck.Explanation
                },
            Trace = new RagTraceDto
            {
                ConfidenceAverageSimilarity = result.Confidence.AverageSimilarity,
                ConfidenceResolvedRatio = result.Confidence.ResolvedRatio,
                ConfidenceSourceCountFactor = result.Confidence.SourceCountFactor,
                SearchDurationMs = result.Trace.SearchDuration.TotalMilliseconds,
                GenerationDurationMs = result.Trace.GenerationDuration?.TotalMilliseconds,
                HallucinationCheckDurationMs = result.Trace.HallucinationCheckDuration?.TotalMilliseconds,
                TotalDurationMs = result.Trace.TotalDuration.TotalMilliseconds
            }
        };

        var sourcesForLog = result.Sources.Select(r => new { r.Ticket.Id, r.Ticket.Title, r.Score });
        var log = new SuggestionLog
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Query = request.Query,
            Answer = result.Answer,
            ConfidencePercentage = result.Confidence.Percentage,
            ShouldEscalate = result.Confidence.ShouldEscalate,
            ConfidenceAverageSimilarity = result.Confidence.AverageSimilarity,
            ConfidenceResolvedRatio = result.Confidence.ResolvedRatio,
            ConfidenceSourceCountFactor = result.Confidence.SourceCountFactor,
            HasHallucination = result.HallucinationCheck?.HasUnsupportedClaims,
            HallucinationExplanation = result.HallucinationCheck?.Explanation,
            SourcesJson = JsonSerializer.Serialize(sourcesForLog),
            SearchDurationMs = result.Trace.SearchDuration.TotalMilliseconds,
            GenerationDurationMs = result.Trace.GenerationDuration?.TotalMilliseconds,
            HallucinationCheckDurationMs = result.Trace.HallucinationCheckDuration?.TotalMilliseconds,
            TotalDurationMs = result.Trace.TotalDuration.TotalMilliseconds
        };
        await _suggestionLogRepository.AddAsync(log, ct);

        return Ok(response);
    }
}
