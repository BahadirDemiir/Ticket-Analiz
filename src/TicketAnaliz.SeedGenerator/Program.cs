using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TicketAnaliz.Infrastructure.Extensions;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var services = new ServiceCollection();
services.AddSemanticKernelServices(configuration);
var provider = services.BuildServiceProvider();

var kernel = provider.GetRequiredService<Kernel>();
var chatService = kernel.GetRequiredService<IChatCompletionService>();

Console.WriteLine("OpenAI baglantisi test ediliyor...");

var response = await chatService.GetChatMessageContentAsync(
    "Merhaba! Bir cumlede kendini tanit.",
    kernel: kernel);

Console.WriteLine("Yanit alindi:");
Console.WriteLine(response.Content);

var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

Console.WriteLine();
Console.WriteLine("Embedding uretimi test ediliyor...");

var vector = await embeddingGenerator.GenerateVectorAsync("SAP RFC baglanti hatasi aliyorum");

Console.WriteLine($"Vektor boyutu: {vector.Length}");
Console.WriteLine($"Ilk 5 deger: {string.Join(", ", vector.ToArray().Take(5))}");

Console.WriteLine();
Console.WriteLine("Qdrant baglantisi test ediliyor...");

var qdrantClient = new QdrantClient("localhost", 6334);
const string testCollectionName = "tickets_smoke_test";

if (!await qdrantClient.CollectionExistsAsync(testCollectionName))
{
    await qdrantClient.CreateCollectionAsync(
        testCollectionName,
        new VectorParams { Size = (ulong)vector.Length, Distance = Distance.Cosine });
    Console.WriteLine($"'{testCollectionName}' koleksiyonu olusturuldu.");
}

var pointId = Guid.NewGuid();
await qdrantClient.UpsertAsync(testCollectionName, new List<PointStruct>
{
    new()
    {
        Id = new PointId { Uuid = pointId.ToString() },
        Vectors = vector.ToArray(),
        Payload = { ["text"] = "SAP RFC baglanti hatasi aliyorum" }
    }
});
Console.WriteLine($"Test noktasi yuklendi (Id: {pointId}).");

var queryResults = await qdrantClient.QueryAsync(testCollectionName, vector.ToArray(), limit: 3);

Console.WriteLine("Arama sonuclari:");
foreach (var result in queryResults)
{
    Console.WriteLine($"  Skor: {result.Score}, Metin: {result.Payload["text"].StringValue}");
}
