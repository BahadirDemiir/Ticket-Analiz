namespace TicketAnaliz.Api.Contracts;

public class TicketSuggestionResponse
{
    public string Answer { get; set; } = default!;

    // "HistoricalTickets" | "WebSearch" | "Escalated"
    public string AnswerSource { get; set; } = default!;

    // Gecmis kayitlardan hesaplanan guven skoru (web cevabinda da bu, web'in guveni degil).
    public double ConfidencePercentage { get; set; }

    // Sonuc olarak BT ekibine yonlendirildi mi (AnswerSource == Escalated).
    public bool ShouldEscalate { get; set; }
    public List<TicketSearchResultDto> Sources { get; set; } = new();
    public List<WebSourceDto> WebSources { get; set; } = new();
    public HallucinationCheckDto? HallucinationCheck { get; set; }
    public RagTraceDto Trace { get; set; } = default!;
}

public class WebSourceDto
{
    public string Title { get; set; } = default!;
    public string Url { get; set; } = default!;
    public double Score { get; set; }
}

public class HallucinationCheckDto
{
    public bool HasUnsupportedClaims { get; set; }
    public string Explanation { get; set; } = default!;
}

public class WebSearchTraceDto
{
    public string? RewrittenQuery { get; set; }
    public int ResultCount { get; set; }
    public int AcceptedCount { get; set; }
    public double? TopScore { get; set; }
    public double DurationMs { get; set; }
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
    public WebSearchTraceDto? WebSearch { get; set; }
}
