namespace TicketAnaliz.Api.Contracts;

public class SuggestionLogSummaryDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Query { get; set; } = default!;
    public double ConfidencePercentage { get; set; }
    public bool ShouldEscalate { get; set; }
    public bool? HasHallucination { get; set; }
    public double TotalDurationMs { get; set; }
}

public class SuggestionLogDetailDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Query { get; set; } = default!;
    public string Answer { get; set; } = default!;
    public double ConfidencePercentage { get; set; }
    public bool ShouldEscalate { get; set; }
    public double ConfidenceAverageSimilarity { get; set; }
    public double ConfidenceResolvedRatio { get; set; }
    public double ConfidenceSourceCountFactor { get; set; }
    public bool? HasHallucination { get; set; }
    public string? HallucinationExplanation { get; set; }
    public List<SuggestionLogSourceDto> Sources { get; set; } = new();
    public double SearchDurationMs { get; set; }
    public double? GenerationDurationMs { get; set; }
    public double? HallucinationCheckDurationMs { get; set; }
    public double TotalDurationMs { get; set; }
}

public class SuggestionLogSourceDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public double Score { get; set; }
}
