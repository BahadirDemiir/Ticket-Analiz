using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Core.Rag;

public record ConfidenceResult(double Percentage, bool ShouldEscalate, string Message);

public class ConfidenceScoreCalculator
{
    private const int TargetSourceCount = 5;
    private const double EscalationThreshold = 60.0;

    public ConfidenceResult Calculate(IReadOnlyList<TicketSearchResult> sources)
    {
        if (sources.Count == 0)
        {
            return new ConfidenceResult(0, true, "Yeterli geçmiş kayıt bulunamadı. Ticket BT ekibine yönlendirilecektir.");
        }

        var avgSimilarity = sources.Average(s => s.Score);
        var resolvedRatio = sources.Count(s => s.Ticket.Status is TicketStatus.Resolved or TicketStatus.Closed) / (double)sources.Count;
        var countFactor = Math.Min(sources.Count, TargetSourceCount) / (double)TargetSourceCount;

        var rawScore = (avgSimilarity * 0.5) + (resolvedRatio * 0.3) + (countFactor * 0.2);
        var percentage = Math.Round(rawScore * 100, 1);
        var shouldEscalate = percentage < EscalationThreshold;

        var message = shouldEscalate
            ? "Yeterli geçmiş kayıt bulunamadı. Ticket BT ekibine yönlendirilecektir."
            : "Öneri geçmiş kayıtlara dayanmaktadır.";

        return new ConfidenceResult(percentage, shouldEscalate, message);
    }
}
