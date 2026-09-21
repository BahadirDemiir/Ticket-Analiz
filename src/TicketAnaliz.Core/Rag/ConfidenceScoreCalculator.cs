using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Core.Rag;

public record ConfidenceResult(
    double Percentage,
    bool ShouldEscalate,
    string Message,
    double AverageSimilarity,
    double ResolvedRatio,
    double SourceCountFactor);

// Bulunan benzer ticket'lara göre güven skoru hesaplar, düşükse BT'ye yönlendirme kararı verir.(benzerlik, çözülmüş ticket oranı ve kaynak sayısı)
public class ConfidenceScoreCalculator
{
    private const int TargetSourceCount = 5;
    private const double EscalationThreshold = 60.0;

    private const double MinimumSimilarityThreshold = 0.55;

    public ConfidenceResult Calculate(IReadOnlyList<TicketSearchResult> sources)
    {
        if (sources.Count == 0)
        {
            return new ConfidenceResult(0, true, "Yeterli geçmiş kayıt bulunamadı. Ticket BT ekibine yönlendirilecektir.", 0, 0, 0);
        }

        var avgSimilarity = sources.Average(s => s.Score);
        var resolvedRatio = sources.Count(s => s.Ticket.Status is TicketStatus.Resolved or TicketStatus.Closed) / (double)sources.Count;
        var countFactor = Math.Min(sources.Count, TargetSourceCount) / (double)TargetSourceCount;

        if (avgSimilarity < MinimumSimilarityThreshold)
        {
            var lowSimilarityPercentage = Math.Round(avgSimilarity * 100, 1);
            return new ConfidenceResult(
                lowSimilarityPercentage,
                true,
                "Bulunan kaynaklar sorguyla yeterince benzer değil. Ticket BT ekibine yönlendirilecektir.",
                avgSimilarity,
                resolvedRatio,
                countFactor);
        }

        var rawScore = (avgSimilarity * 0.6) + (resolvedRatio * 0.25) + (countFactor * 0.15);
        var percentage = Math.Round(rawScore * 100, 1);
        var shouldEscalate = percentage < EscalationThreshold;

        var message = shouldEscalate
            ? "Yeterli geçmiş kayıt bulunamadı. Ticket BT ekibine yönlendirilecektir."
            : "Öneri geçmiş kayıtlara dayanmaktadır.";

        return new ConfidenceResult(percentage, shouldEscalate, message, avgSimilarity, resolvedRatio, countFactor);
    }
}
