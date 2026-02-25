using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using PurviewConsortium.Core.Interfaces;
using PurviewConsortium.Infrastructure.Data;
using PurviewConsortium.Infrastructure.Repositories;
using PurviewConsortium.Infrastructure.Services;

namespace PurviewConsortium.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool useDevelopmentServices = false)
    {
        // Database
        if (useDevelopmentServices)
        {
            services.AddDbContext<ConsortiumDbContext>(options =>
                options.UseInMemoryDatabase("PurviewConsortiumDev"));
        }
        else
        {
            services.AddDbContext<ConsortiumDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(ConsortiumDbContext).Assembly.FullName)));
        }

        // Repositories
        services.AddScoped<IInstitutionRepository, InstitutionRepository>();
        services.AddScoped<IDataProductRepository, DataProductRepository>();
        services.AddScoped<IAccessRequestRepository, AccessRequestRepository>();
        services.AddScoped<ISyncHistoryRepository, SyncHistoryRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // Services
        services.AddScoped<IPurviewScannerService, PurviewScannerService>();
        services.AddScoped<IPurviewWorkflowService, PurviewWorkflowService>();
        services.AddScoped<IFabricShortcutService, FabricShortcutService>();
        services.AddScoped<ISyncOrchestrator, SyncOrchestrator>();
        services.AddScoped<INotificationService, EmailNotificationService>();

        // Catalog search — always use database-backed search for the POC.
        // Switch to AzureAISearchService once the search index is populated via sync.
        services.AddScoped<ICatalogSearchService, InMemoryCatalogSearchService>();

        // HTTP clients with retry policy
        services.AddHttpClient("Purview")
            .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services.AddHttpClient("Fabric")
            .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        return services;
    }
}
