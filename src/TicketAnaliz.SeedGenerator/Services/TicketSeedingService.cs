using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.SeedGenerator.Models;

namespace TicketAnaliz.SeedGenerator.Services;

public class TicketSeedingService // bu servis, kendi oluşturduğumuz JSON dosyasından ticket verilerini okuyup SQL Server ve Qdrant'a yükler.
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

        // Her senaryo grubunun (ayni scenarioKey'i paylasan varyasyonlar) SON varyasyonunu
        // bilerek Qdrant'a yuklemiyoruz - bu, evaluation'da gercek bir "daha once hic
        // gorulmemis soru" testi yapabilmemiz icin tutulan (held-out) bir test sorgusu olacak.
        // SQL'e hepsi kaydediliyor, sadece vector store'a girip girmedigi farkli.
        var groupedByScenario = seedFile.Tickets.GroupBy(t => t.ScenarioKey).ToList();

        var now = DateTime.UtcNow;
        var processed = 0;
        var heldOutCount = 0;
        var totalCount = seedFile.Tickets.Count;

        foreach (var group in groupedByScenario)
        {
            var variations = group.ToList();

            // Held-out olarak, cozumu OLAN bir varyasyonu seciyoruz - yoksa "dogru cozum
            // onerme orani" metrigini olcecek referans metnimiz olmaz. Cozumu olan yoksa
            // (butun grup cozulmemisse) son varyasyonu kullaniyoruz.
            var heldOutIndex = variations.FindLastIndex(v => !string.IsNullOrWhiteSpace(v.Resolution));
            if (heldOutIndex < 0)
            {
                heldOutIndex = variations.Count - 1;
            }

            for (var i = 0; i < variations.Count; i++)
            {
                var dto = variations[i];
                var isHeldOut = i == heldOutIndex;

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
                    IsSynthetic = true,
                    ScenarioKey = dto.ScenarioKey,
                    IsInVectorStore = !isHeldOut
                };

                await _ticketRepository.AddAsync(ticket);

                if (isHeldOut)
                {
                    heldOutCount++;
                }
                else
                {
                    // Only the problem description is embedded, not the resolution: users query with
                    // symptoms, not solutions, so keeping the vector space symmetric with queries avoids
                    // solution-vocabulary (e.g. "zaman aşımı" in an unrelated fix) pulling in false matches.
                    var embeddingSourceText = $"{ticket.Title} {ticket.Description}";

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
                }

                processed++;
                if (processed % 10 == 0)
                {
                    Console.WriteLine($"  {processed}/{totalCount} islendi...");
                }
            }
        }

        Console.WriteLine($"Tamamlandi: {processed} ticket SQL Server'a yazildi, {processed - heldOutCount} tanesi Qdrant'a yuklendi, {heldOutCount} tanesi test icin tutuldu (held-out).");
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
