using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Qdrant.Client;
using TicketAnaliz.Core.Repositories;

namespace TicketAnaliz.SeedGenerator.Services;

public class QuickSearchService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly QdrantClient _qdrantClient;
    private readonly RerankingService _rerankingService;
    private readonly string _collectionName;

    public QuickSearchService(
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

    public async Task SearchAsync(string queryText, int topK = 5)
    {
        Console.WriteLine($"Sorgu: \"{queryText}\"");
        Console.WriteLine("Embedding uretiliyor ve Qdrant'ta araniyor...");

        var queryVector = await _embeddingGenerator.GenerateVectorAsync(queryText);

        // Re-ranking'in elemesi icin ihtiyac duyulandan biraz daha genis bir aday havuzu cekiyoruz.
        var candidatePoolSize = topK * 2;
        var results = await _qdrantClient.QueryAsync(_collectionName, queryVector.ToArray(), limit: (ulong)candidatePoolSize);

        var ticketIds = results.Select(r => Guid.Parse(r.Id.Uuid)).ToList();
        var tickets = await _ticketRepository.GetByIdsAsync(ticketIds);
        var ticketsById = tickets.ToDictionary(t => t.Id);

        var indexedResults = results
            .Select((r, i) => (Index: i, Result: r, Ticket: ticketsById.GetValueOrDefault(Guid.Parse(r.Id.Uuid))))
            .Where(x => x.Ticket is not null)
            .ToList();

        Console.WriteLine($"Vector search {indexedResults.Count} aday buldu, re-ranking ile alakasizlar eleniyor...");
        Console.WriteLine();

        var candidates = indexedResults
            .Select(x => new RerankCandidate(x.Index, x.Ticket!.Title, x.Ticket!.Description))
            .ToList();

        var relevantIndices = (await _rerankingService.GetRelevantIndicesAsync(queryText, candidates)).ToHashSet();

        var finalResults = indexedResults.Where(x => relevantIndices.Contains(x.Index)).Take(topK).ToList();
        var eliminatedResults = indexedResults.Where(x => !relevantIndices.Contains(x.Index)).ToList();

        foreach (var (_, result, ticket) in finalResults)
        {
            Console.WriteLine($"[Skor: {result.Score:F4}] ({ticket!.Status}) {ticket.Title}");
            Console.WriteLine($"  Kategori: {ticket.Category} | Departman: {ticket.Department}");
            if (ticket.Resolution is not null)
            {
                Console.WriteLine($"  Cozum: {ticket.Resolution}");
            }
            Console.WriteLine();
        }

        if (eliminatedResults.Count > 0)
        {
            Console.WriteLine("--- Re-ranking tarafindan elenenler (vector search bulmustu ama alakasiz bulundu) ---");
            foreach (var (_, result, ticket) in eliminatedResults)
            {
                Console.WriteLine($"[Skor: {result.Score:F4}] {ticket!.Title}");
            }
        }
    }
}
