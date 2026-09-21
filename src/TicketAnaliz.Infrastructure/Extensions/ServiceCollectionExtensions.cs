using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using TicketAnaliz.Core.Auth;
using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Rag;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.Core.Search;
using TicketAnaliz.Infrastructure.Auth;
using TicketAnaliz.Infrastructure.Context;
using TicketAnaliz.Infrastructure.Rag;
using TicketAnaliz.Infrastructure.Repositories;
using TicketAnaliz.Infrastructure.Search;

namespace TicketAnaliz.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ISuggestionLogRepository, SuggestionLogRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<IUserService, UserService>();

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

        services.AddSingleton(sp => sp.GetRequiredService<Kernel>()
            .GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());

        return services;
    }

    public static IServiceCollection AddQdrantServices(this IServiceCollection services, IConfiguration configuration)
    {
        var host = configuration["Qdrant:Host"]
            ?? throw new InvalidOperationException("Qdrant:Host is not configured.");
        var port = int.Parse(configuration["Qdrant:Port"]
            ?? throw new InvalidOperationException("Qdrant:Port is not configured."));

        services.AddSingleton(_ => new QdrantClient(host, port));

        return services;
    }

    public static IServiceCollection AddTicketSearchServices(this IServiceCollection services)
    {
        services.AddScoped<RerankingService>();
        services.AddScoped<ITicketSearchService, TicketSearchService>();

        return services;
    }

    public static IServiceCollection AddWebSearchServices(this IServiceCollection services)
    {
        services.AddHttpClient<IWebSearchService, TavilyWebSearchService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        return services;
    }

    public static IServiceCollection AddRagServices(this IServiceCollection services)
    {
        // Orkestrasyon web fallback'e de bagli, o yuzden her host'ta (Api/Evaluation/SeedGenerator) kayitli olmali.
        services.AddWebSearchServices();
        services.AddScoped<WebQueryRewriter>();
        services.AddScoped<WebRelevanceFilter>();
        services.AddScoped<IWebFallbackService, WebFallbackService>();
        services.AddScoped<RagPromptBuilder>();
        services.AddScoped<ConfidenceScoreCalculator>();
        services.AddScoped<IHallucinationChecker, HallucinationChecker>();
        services.AddScoped<IRagOrchestrationService, RagOrchestrationService>();

        return services;
    }
}
