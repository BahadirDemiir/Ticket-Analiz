using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.SeedGenerator.Models;

namespace TicketAnaliz.SeedGenerator.Services;

public class TicketSeedingService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly QdrantClient _qdrantClient;
    private readonly string _collectionName;

    public TicketSeedingService(
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

    public async Task SeedFromJsonAsync(string jsonFilePath)
    {
        var json = await File.ReadAllTextAsync(jsonFilePath);
        var seedFile = JsonSerializer.Deserialize<SeedTicketFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Seed JSON dosyasi okunamadi veya bos.");

        Console.WriteLine($"{seedFile.Tickets.Count} adet ticket okundu, isleniyor...");

        var now = DateTime.UtcNow;
        var processed = 0;

        foreach (var dto in seedFile.Tickets)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Description = dto.Description,
                Category = dto.Category,
                Department = Enum.Parse<Department>(dto.Department),
                Resolution = dto.Resolution,
                Status = Enum.Parse<TicketStatus>(dto.Status),
                Priority = dto.Priority,
                CreatedDate = now.AddDays(-dto.CreatedDaysAgo),
                ResolvedDate = dto.ResolvedDaysAgo.HasValue ? now.AddDays(-dto.ResolvedDaysAgo.Value) : null,
                IsSynthetic = true
            };

            await _ticketRepository.AddAsync(ticket);

            var embeddingSourceText = string.Join(" ", new[] { ticket.Title, ticket.Description, ticket.Resolution }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            var vector = await _embeddingGenerator.GenerateVectorAsync(embeddingSourceText);

            await EnsureCollectionExistsAsync(vector.Length);

            await _qdrantClient.UpsertAsync(_collectionName, new List<PointStruct>
            {
                new()
                {
                    Id = new PointId { Uuid = ticket.Id.ToString() },
                    Vectors = vector.ToArray(),
                    Payload =
                    {
                        ["category"] = ticket.Category,
                        ["department"] = ticket.Department.ToString(),
                        ["status"] = ticket.Status.ToString(),
                        ["scenarioKey"] = dto.ScenarioKey,
                        ["createdDate"] = new DateTimeOffset(ticket.CreatedDate).ToUnixTimeSeconds()
                    }
                }
            });

            processed++;
            if (processed % 10 == 0)
            {
                Console.WriteLine($"  {processed}/{seedFile.Tickets.Count} islendi...");
            }
        }

        Console.WriteLine($"Tamamlandi: {processed} ticket SQL Server'a ve Qdrant'a yuklendi.");
    }

    private bool _collectionEnsured;

    private async Task EnsureCollectionExistsAsync(int vectorSize)
    {
        if (_collectionEnsured)
        {
            return;
        }

        if (!await _qdrantClient.CollectionExistsAsync(_collectionName))
        {
            await _qdrantClient.CreateCollectionAsync(
                _collectionName,
                new VectorParams { Size = (ulong)vectorSize, Distance = Distance.Cosine });
            Console.WriteLine($"'{_collectionName}' koleksiyonu olusturuldu (boyut: {vectorSize}).");
        }

        _collectionEnsured = true;
    }
}
