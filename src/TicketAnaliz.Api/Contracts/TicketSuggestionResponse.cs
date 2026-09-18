namespace TicketAnaliz.Api.Contracts;

public class TicketSuggestionResponse
{
    public string Answer { get; set; } = default!;
    public double ConfidencePercentage { get; set; }
    public bool ShouldEscalate { get; set; }
    public List<TicketSearchResultDto> Sources { get; set; } = new();
    public HallucinationCheckDto? HallucinationCheck { get; set; }
    public RagTraceDto Trace { get; set; } = default!;
}

public class HallucinationCheckDto
{
    public bool HasUnsupportedClaims { get; set; }
    public string Explanation { get; set; } = default!;
}

public class RagTraceDto
{
    public double ConfidenceAverageSimilarity { get; set; }
    public double ConfidenceResolvedRatio { get; set; }
    public double ConfidenceSourceCountFactor { get; set; }
    public double SearchDurationMs { get; set; }
    public double? GenerationDurationMs { get; set; }
    public double? HallucinationCheckDurationMs { get; set; }
    public double TotalDurationMs { get; set; }
}
