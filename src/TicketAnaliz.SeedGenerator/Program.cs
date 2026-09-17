using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
services.AddSingleton(sp => sp.GetRequiredService<Microsoft.SemanticKernel.Kernel>()
    .GetRequiredService<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>());
services.AddScoped<TicketSeedingService>();
services.AddScoped<RerankingService>();
services.AddScoped<QuickSearchService>();

var provider = services.BuildServiceProvider();

using var scope = provider.CreateScope();

if (args.Length > 0 && args[0] == "--query")
{
    var queryText = string.Join(" ", args.Skip(1));
    var searchService = scope.ServiceProvider.GetRequiredService<QuickSearchService>();
    await searchService.SearchAsync(queryText);
}
else
{
    var seeder = scope.ServiceProvider.GetRequiredService<TicketSeedingService>();
    var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "synthetic_tickets.json");
    await seeder.SeedFromJsonAsync(jsonPath);
}
