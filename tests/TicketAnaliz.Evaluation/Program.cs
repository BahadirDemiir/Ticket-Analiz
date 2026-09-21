using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketAnaliz.Evaluation;
using TicketAnaliz.Infrastructure.Extensions;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

// Kullanim:
//   dotnet run --project tests/TicketAnaliz.Evaluation -- 20                      -> gecmis ticket olcumu (held-out ornek sayisi)
//   dotnet run --project tests/TicketAnaliz.Evaluation -- --web [tekrar] [etiket] -> web fallback olcumu
var webMode = args.Length > 0 && args[0] == "--web";
var webRepeats = webMode && args.Length > 1 && int.TryParse(args[1], out var r) ? r : 3;
var webLabel = webMode && args.Length > 2 ? args[2] : "mevcut";
var sampleCount = !webMode && args.Length > 0 && int.TryParse(args[0], out var parsed) ? parsed : 10;

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddInfrastructureServices(configuration);
services.AddSemanticKernelServices(configuration);
services.AddQdrantServices(configuration);
services.AddTicketSearchServices();
services.AddRagServices();
services.AddScoped<CorrectnessJudge>();
services.AddScoped<EvaluationRunner>();
services.AddScoped<WebRelevanceJudge>();
services.AddScoped<WebFallbackEvaluator>();

var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

if (webMode)
{
    var webEvaluator = scope.ServiceProvider.GetRequiredService<WebFallbackEvaluator>();
    await webEvaluator.RunAsync(webRepeats, webLabel);
}
else
{
    var runner = scope.ServiceProvider.GetRequiredService<EvaluationRunner>();
    await runner.RunAsync(sampleCount);
}
