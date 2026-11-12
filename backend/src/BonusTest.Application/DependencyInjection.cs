using Microsoft.Extensions.DependencyInjection;
using BonusTest.Application.Catalogs;

namespace BonusTest.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CatalogService>();

        // registra aquí los servicios del módulo de bonos.

        return services;
    }
}
