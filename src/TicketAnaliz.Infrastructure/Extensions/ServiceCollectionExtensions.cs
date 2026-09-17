using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.Infrastructure.Context;
using TicketAnaliz.Infrastructure.Repositories;

namespace TicketAnaliz.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ITicketRepository, TicketRepository>();

        return services;
    }

    public static IServiceCollection AddSemanticKernelServices(this IServiceCollection services, IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
        var chatModel = configuration["OpenAI:ChatModel"]
            ?? throw new InvalidOperationException("OpenAI:ChatModel is not configured.");
        var embeddingModel = configuration["OpenAI:EmbeddingModel"]
            ?? throw new InvalidOperationException("OpenAI:EmbeddingModel is not configured.");

        services.AddSingleton(_ =>
        {
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(chatModel, apiKey);

#pragma warning disable SKEXP0010 // AddOpenAIEmbeddingGenerator is experimental; it's SK's recommended forward path replacing the deprecated AddOpenAITextEmbeddingGeneration.
            builder.AddOpenAIEmbeddingGenerator(embeddingModel, apiKey);
#pragma warning restore SKEXP0010

            return builder.Build();
        });

        return services;
    }
}
