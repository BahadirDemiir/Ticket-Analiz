using Microsoft.EntityFrameworkCore;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.Infrastructure.Context;
using TicketAnaliz.Infrastructure.Repositories;

namespace TicketAnaliz.Api.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ITicketRepository, TicketRepository>();

        return services;
    }
}
