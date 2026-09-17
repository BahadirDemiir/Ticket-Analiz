using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
