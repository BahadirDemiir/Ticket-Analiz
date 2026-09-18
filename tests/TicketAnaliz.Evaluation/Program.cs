using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketAnaliz.Evaluation;
using TicketAnaliz.Infrastructure.Extensions;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var sampleCount = args.Length > 0 && int.TryParse(args[0], out var parsed) ? parsed : 10;

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddInfrastructureServices(configuration);
services.AddSemanticKernelServices(configuration);
services.AddQdrantServices(configuration);
services.AddTicketSearchServices();
services.AddRagServices();
services.AddScoped<CorrectnessJudge>();
services.AddScoped<EvaluationRunner>();

var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var runner = scope.ServiceProvider.GetRequiredService<EvaluationRunner>();
await runner.RunAsync(sampleCount);
