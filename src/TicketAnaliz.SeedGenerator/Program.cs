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
    Console.WriteLine();
    Console.WriteLine("=== LLM CEVABI ===");
    Console.WriteLine(result.Answer);
    Console.WriteLine();
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
