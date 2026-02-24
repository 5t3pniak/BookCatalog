using BookCatalog.ApplicationCore.Jobs;
using BookCatalog.ApplicationCore.QueryHandlers;
using BookCatalog.Integrations.OpenBooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Internal;
using TickerQ.DependencyInjection;

namespace BookCatalog.ApplicationCore.DependencyInjection;

public static class ApplicationCoreExtensions
{
    public static IServiceCollection AddApplicationCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenBooksIntegration(configuration);
        services.AddTransient<CatalogSyncJob>();
        services.AddTransient<RemainingSyncAuthorsDataSyncJob>();
        services.AddTransient<CatalogSync>();
        services.AddTransient<IBooksHandler, BooksHandler>();
        services.AddTransient<IAuthorsHandler, AuthorsHandler>();
        services.AddSingleton<ISystemClock, SystemClock>();
      
        services.AddTickerQ(options =>
        {
            options.UseTickerSeeder(async manager =>
                await WorkflowJobDefinitions.TriggerCatalogSyncAsync(manager));
        });

        return services;
    }
    
    public static IHost UseApplicationCore(this IHost host)
    {
        host.UseTickerQ();
        return host;
    }
}