using Microsoft.Extensions.DependencyInjection;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Catalogs;

namespace BonusTest.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CatalogService>();
        services.AddScoped<BonusService>();

        return services;
    }
}
