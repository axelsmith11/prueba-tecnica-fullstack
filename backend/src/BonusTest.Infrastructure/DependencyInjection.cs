using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BonusTest.Application.Catalogs;
using BonusTest.Infrastructure.Persistence;
using BonusTest.Infrastructure.Repositories;

namespace BonusTest.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("BonusTest")));

        services.AddScoped<ICatalogRepository, CatalogRepository>();

        // registra aquí los repositorios del módulo de bonos.

        return services;
    }
}
