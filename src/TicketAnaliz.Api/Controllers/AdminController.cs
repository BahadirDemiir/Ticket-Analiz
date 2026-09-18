using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TicketAnaliz.Api.Contracts;
using TicketAnaliz.Core.Repositories;

namespace TicketAnaliz.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly ISuggestionLogRepository _suggestionLogRepository;

    public AdminController(ISuggestionLogRepository suggestionLogRepository)
    {
        _suggestionLogRepository = suggestionLogRepository;
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<SuggestionLogSummaryDto>>> GetHistory([FromQuery] int count, CancellationToken ct)
    {
        var take = count > 0 ? count : 50;
        var logs = await _suggestionLogRepository.GetRecentAsync(take, ct);

        var summaries = logs.Select(l => new SuggestionLogSummaryDto
        {
            Id = l.Id,
            CreatedAt = l.CreatedAt,
            Query = l.Query,
            ConfidencePercentage = l.ConfidencePercentage,
            ShouldEscalate = l.ShouldEscalate,
            HasHallucination = l.HasHallucination,
            TotalDurationMs = l.TotalDurationMs
        }).ToList();

        return Ok(summaries);
    }

    [HttpGet("history/{id:guid}")]
    public async Task<ActionResult<SuggestionLogDetailDto>> GetHistoryDetail(Guid id, CancellationToken ct)
    {
        var log = await _suggestionLogRepository.GetByIdAsync(id, ct);
        if (log is null)
        {
            return NotFound();
        }

        var sources = JsonSerializer.Deserialize<List<JsonElement>>(log.SourcesJson) ?? new List<JsonElement>();

        var detail = new SuggestionLogDetailDto
        {
            Id = log.Id,
            CreatedAt = log.CreatedAt,
            Query = log.Query,
            Answer = log.Answer,
            ConfidencePercentage = log.ConfidencePercentage,
            ShouldEscalate = log.ShouldEscalate,
            ConfidenceAverageSimilarity = log.ConfidenceAverageSimilarity,
            ConfidenceResolvedRatio = log.ConfidenceResolvedRatio,
            ConfidenceSourceCountFactor = log.ConfidenceSourceCountFactor,
            HasHallucination = log.HasHallucination,
            HallucinationExplanation = log.HallucinationExplanation,
            Sources = sources.Select(s => new SuggestionLogSourceDto
            {
                Id = s.GetProperty("Id").GetGuid(),
                Title = s.GetProperty("Title").GetString() ?? "",
                Score = s.GetProperty("Score").GetDouble()
            }).ToList(),
            SearchDurationMs = log.SearchDurationMs,
            GenerationDurationMs = log.GenerationDurationMs,
            HallucinationCheckDurationMs = log.HallucinationCheckDurationMs,
            TotalDurationMs = log.TotalDurationMs
        };

        return Ok(detail);
    }
}
