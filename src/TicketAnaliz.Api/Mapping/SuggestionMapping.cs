using System.Text.Json;
using TicketAnaliz.Api.Contracts;
using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Rag;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Api.Mapping;

// Controller'lar sadece istegi alip sonucu donsun diye, Core sonuclarini DTO'ya ve log kaydina ceviren kod burada.
public static class SuggestionMapping
{
    public static TicketSearchResultDto ToDto(this TicketSearchResult r) => new()
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
    };

    public static TicketSuggestionResponse ToResponse(this RagSuggestionResult result)
    {
        var web = result.Trace.WebSearch;

        return new TicketSuggestionResponse
        {
            Answer = result.Answer,
            AnswerSource = result.Source.ToString(),
            ConfidencePercentage = result.Confidence.Percentage,
            // Web cevabi geldiyse gecmis kayit guveni dusuk olsa bile kullanici BT'ye yonlendirilmiyor,
            // o yuzden "yonlendirildi mi" bilgisi guven skorundan degil cevabin kaynagindan turetiliyor.
            ShouldEscalate = result.Source == AnswerSource.Escalated,
            Sources = result.Sources.Select(s => s.ToDto()).ToList(),
            WebSources = result.WebSourceDtos(),
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
                TotalDurationMs = result.Trace.TotalDuration.TotalMilliseconds,
                WebSearch = web is null
                    ? null
                    : new WebSearchTraceDto
                    {
                        RewrittenQuery = web.RewrittenQuery,
                        ResultCount = web.ResultCount,
                        AcceptedCount = web.AcceptedCount,
                        TopScore = web.TopScore,
                        DurationMs = web.Duration.TotalMilliseconds
                    }
            }
        };
    }

    public static SuggestionLog ToLog(this RagSuggestionResult result, string query)
    {
        var web = result.Trace.WebSearch;

        return new SuggestionLog
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Query = query,
            Answer = result.Answer,
            AnswerSource = result.Source,
            ShouldEscalate = result.Source == AnswerSource.Escalated,
            ConfidencePercentage = result.Confidence.Percentage,
            ConfidenceAverageSimilarity = result.Confidence.AverageSimilarity,
            ConfidenceResolvedRatio = result.Confidence.ResolvedRatio,
            ConfidenceSourceCountFactor = result.Confidence.SourceCountFactor,
            HasHallucination = result.HallucinationCheck?.HasUnsupportedClaims,
            HallucinationExplanation = result.HallucinationCheck?.Explanation,
            SourcesJson = JsonSerializer.Serialize(result.Sources.Select(s => new { s.Ticket.Id, s.Ticket.Title, s.Score })),
            WebSourcesJson = JsonSerializer.Serialize(result.WebSourceDtos()),
            WebSearchQuery = web?.RewrittenQuery,
            WebSearchResultCount = web?.ResultCount,
            WebSearchAcceptedCount = web?.AcceptedCount,
            WebSearchDurationMs = web?.Duration.TotalMilliseconds,
            SearchDurationMs = result.Trace.SearchDuration.TotalMilliseconds,
            GenerationDurationMs = result.Trace.GenerationDuration?.TotalMilliseconds,
            HallucinationCheckDurationMs = result.Trace.HallucinationCheckDuration?.TotalMilliseconds,
            TotalDurationMs = result.Trace.TotalDuration.TotalMilliseconds
        };
    }

    private static List<WebSourceDto> WebSourceDtos(this RagSuggestionResult result) =>
        (result.WebSources ?? Array.Empty<WebSearchResult>())
            .Select(w => new WebSourceDto { Title = w.Title, Url = w.Url, Score = w.Score })
            .ToList();
}
