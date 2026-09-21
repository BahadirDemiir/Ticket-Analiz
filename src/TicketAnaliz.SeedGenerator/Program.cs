using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketAnaliz.Core.Rag;
using TicketAnaliz.Infrastructure.Extensions;
using TicketAnaliz.SeedGenerator.Services;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddInfrastructureServices(configuration);
services.AddSemanticKernelServices(configuration);
services.AddQdrantServices(configuration);
services.AddTicketSearchServices();
services.AddWebSearchServices();
services.AddRagServices();
services.AddScoped<TicketSeedingService>();
services.AddScoped<QuickSearchService>();

var provider = services.BuildServiceProvider();

using var scope = provider.CreateScope();

if (args.Length > 0 && args[0] == "--query")
{
    var queryText = string.Join(" ", args.Skip(1));
    var searchService = scope.ServiceProvider.GetRequiredService<QuickSearchService>();
    await searchService.SearchAsync(queryText);
}
else if (args.Length > 0 && args[0] == "--web")
{
    var queryText = string.Join(" ", args.Skip(1));
    var webSearch = scope.ServiceProvider.GetRequiredService<TicketAnaliz.Core.Search.IWebSearchService>();

    Console.WriteLine($"Web aramasi: \"{queryText}\"");
    var webResults = await webSearch.SearchAsync(queryText);
    Console.WriteLine($"{webResults.Count} sonuc bulundu.");
    foreach (var r in webResults)
    {
        Console.WriteLine($"[Skor: {r.Score:F3}] {r.Title}");
        Console.WriteLine($"  {r.Url}");
        Console.WriteLine($"  {r.Content[..Math.Min(150, r.Content.Length)].ReplaceLineEndings(" ")}...");
    }
}
else if (args.Length > 0 && args[0] == "--suggest")
{
    var queryText = string.Join(" ", args.Skip(1));
    var ragService = scope.ServiceProvider.GetRequiredService<IRagOrchestrationService>();

    Console.WriteLine($"Sorgu: \"{queryText}\"");
    Console.WriteLine("Cozum onerisi uretiliyor...");
    Console.WriteLine();

    var result = await ragService.GenerateSuggestionAsync(queryText);

    Console.WriteLine($"=== GUVEN SKORU: %{result.Confidence.Percentage} ===");
    Console.WriteLine(result.Confidence.ShouldEscalate ? "DURUM: BT ekibine yonlendirilecek" : "DURUM: Cevap gosteriliyor");
    Console.WriteLine($"  Ortalama Benzerlik: {result.Confidence.AverageSimilarity:F3}  (agirlik: %50)");
    Console.WriteLine($"  Cozulmus Orani:     {result.Confidence.ResolvedRatio:F3}  (agirlik: %30)");
    Console.WriteLine($"  Kaynak Sayisi Fakt.: {result.Confidence.SourceCountFactor:F3}  (agirlik: %20)");
    Console.WriteLine();

    Console.WriteLine("=== SISTEM IZI (TRACE) ===");
    Console.WriteLine($"  Arama suresi:               {result.Trace.SearchDuration.TotalMilliseconds:F0} ms");
    if (result.Trace.GenerationDuration is not null)
    {
        Console.WriteLine($"  LLM cevap uretme suresi:    {result.Trace.GenerationDuration.Value.TotalMilliseconds:F0} ms");
    }
    if (result.Trace.HallucinationCheckDuration is not null)
    {
        Console.WriteLine($"  Hallucination kontrol suresi: {result.Trace.HallucinationCheckDuration.Value.TotalMilliseconds:F0} ms");
    }
    Console.WriteLine($"  Toplam sure:                {result.Trace.TotalDuration.TotalMilliseconds:F0} ms");
    Console.WriteLine();
    Console.WriteLine("=== LLM CEVABI ===");
    Console.WriteLine(result.Answer);
    Console.WriteLine();

    if (result.HallucinationCheck is not null)
    {
        Console.WriteLine($"=== HALLUCINATION KONTROLU: {(result.HallucinationCheck.HasUnsupportedClaims ? "SUPHELI" : "TEMIZ")} ===");
        Console.WriteLine(result.HallucinationCheck.Explanation);
        Console.WriteLine();
    }

    Console.WriteLine("=== KULLANILAN KAYNAKLAR ===");
    foreach (var source in result.Sources)
    {
        Console.WriteLine($"[Skor: {source.Score:F4}] ({source.Ticket.Status}) {source.Ticket.Id} - {source.Ticket.Title}");
    }
}
else
{
    var seeder = scope.ServiceProvider.GetRequiredService<TicketSeedingService>();
    var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "synthetic_tickets.json");
    await seeder.SeedFromJsonAsync(jsonPath);
}
