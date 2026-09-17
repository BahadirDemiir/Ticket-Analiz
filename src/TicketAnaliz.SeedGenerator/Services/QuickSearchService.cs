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
    private readonly string _collectionName;

    public QuickSearchService(
        ITicketRepository ticketRepository,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        QdrantClient qdrantClient,
        IConfiguration configuration)
    {
        _ticketRepository = ticketRepository;
        _embeddingGenerator = embeddingGenerator;
        _qdrantClient = qdrantClient;
        _collectionName = configuration["Qdrant:CollectionName"]
            ?? throw new InvalidOperationException("Qdrant:CollectionName is not configured.");
    }

    public async Task SearchAsync(string queryText, int topK = 5)
    {
        Console.WriteLine($"Sorgu: \"{queryText}\"");
        Console.WriteLine("Embedding uretiliyor ve Qdrant'ta araniyor...");
        Console.WriteLine();

        var queryVector = await _embeddingGenerator.GenerateVectorAsync(queryText);
        var results = await _qdrantClient.QueryAsync(_collectionName, queryVector.ToArray(), limit: (ulong)topK);

        var ticketIds = results.Select(r => Guid.Parse(r.Id.Uuid)).ToList();
        var tickets = await _ticketRepository.GetByIdsAsync(ticketIds);
        var ticketsById = tickets.ToDictionary(t => t.Id);

        foreach (var result in results)
        {
            var ticketId = Guid.Parse(result.Id.Uuid);
            if (!ticketsById.TryGetValue(ticketId, out var ticket))
            {
                continue;
            }

            Console.WriteLine($"[Skor: {result.Score:F4}] ({ticket.Status}) {ticket.Title}");
            Console.WriteLine($"  Kategori: {ticket.Category} | Departman: {ticket.Department}");
            if (ticket.Resolution is not null)
            {
                Console.WriteLine($"  Cozum: {ticket.Resolution}");
            }
            Console.WriteLine();
        }
    }
}
