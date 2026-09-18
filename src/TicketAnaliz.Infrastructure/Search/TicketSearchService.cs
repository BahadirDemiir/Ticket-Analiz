using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Qdrant.Client;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.Core.Search;

namespace TicketAnaliz.Infrastructure.Search;

public class TicketSearchService : ITicketSearchService // orkestra þefi: ticket bilgilerini repository'den alýr, embedding oluþturur, Qdrant'ta arama yapar ve reranking uygular
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly QdrantClient _qdrantClient;
    private readonly RerankingService _rerankingService;
    private readonly string _collectionName;

    public TicketSearchService(
        ITicketRepository ticketRepository,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        QdrantClient qdrantClient,
        RerankingService rerankingService,
        IConfiguration configuration)
    {
        _ticketRepository = ticketRepository;
        _embeddingGenerator = embeddingGenerator;
        _qdrantClient = qdrantClient;
        _rerankingService = rerankingService;
        _collectionName = configuration["Qdrant:CollectionName"]
            ?? throw new InvalidOperationException("Qdrant:CollectionName is not configured.");
    }

    public async Task<IReadOnlyList<TicketSearchResult>> SearchAsync(string queryText, int topK = 5, CancellationToken ct = default)
    {
        // Kullanýcýnýn sorgusunu embedding vektörüne dönüþtür
        var queryVector = await _embeddingGenerator.GenerateVectorAsync(queryText, cancellationToken: ct);

        // Re-ranking'in elemesi icin ihtiyac duyulandan biraz daha genis bir aday havuzu cekilir
        var candidatePoolSize = topK * 2;
        // Qdrant'ta embedding vektörüne en yakýn aday ticket'larý bulur 
        var qdrantResults = await _qdrantClient.QueryAsync(_collectionName, queryVector.ToArray(), limit: (ulong)candidatePoolSize, cancellationToken: ct);
   
        var ticketIds = qdrantResults.Select(r => Guid.Parse(r.Id.Uuid)).ToList();
        // Ticket bilgilerini( baþlýk, açýklama, kategori vb.) database'den çeker
        var tickets = await _ticketRepository.GetByIdsAsync(ticketIds, ct);
        var ticketsById = tickets.ToDictionary(t => t.Id);
       
        var indexedResults = qdrantResults
            .Select((r, i) => (Index: i, Result: r, Ticket: ticketsById.GetValueOrDefault(Guid.Parse(r.Id.Uuid))))
            .Where(x => x.Ticket is not null)
            .ToList();

        var candidates = indexedResults
            .Select(x => new RerankCandidate(x.Index, x.Ticket!.Title, x.Ticket!.Description))
            .ToList();
        // RerankingService ile aday ticket'larin kullanýcýnýn sorgusuyla ne kadar alakalý olduðunu belirler, alakasýz olanlarý eleriz
        var relevantIndices = (await _rerankingService.GetRelevantIndicesAsync(queryText, candidates)).ToHashSet();
        // Sonuçlarý alaka sýrasýna göre filtreler ve belirlediðimiz adet kadar döndürür
        return indexedResults
            .Where(x => relevantIndices.Contains(x.Index))
            .Take(topK)
            .Select(x => new TicketSearchResult(x.Ticket!, x.Result.Score))
            .ToList();
    }
}
