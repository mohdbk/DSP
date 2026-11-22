using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Messaging;
using Dsp.BuildingBlocks.Web;
using Dsp.Contracts;
using Dsp.EServices.Application;
using Dsp.EServices.Infrastructure.Catalog;
using Dsp.EServices.Infrastructure.Clients;
using Dsp.EServices.Infrastructure.Persistence;
using Dsp.EServices.Infrastructure.Workflows;
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.EServices.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEServicesInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Own database for e-service data; a separate database for the workflow engine.
        services.AddDspDbContext<EServicesDbContext, EServicesSqlServerDbContext, EServicesPostgresDbContext>(configuration, "EServices");
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IReviewTaskRepository, ReviewTaskRepository>();
        services.AddScoped<IAiAssessmentRepository, AiAssessmentRepository>();

        // Service definitions live in the database (edited in the service builder); the shipped ones seed it.
        services.AddScoped<ServiceVersionRepository>();
        services.AddScoped<IServiceVersionRepository>(sp => sp.GetRequiredService<ServiceVersionRepository>());
        services.AddScoped<IServiceCatalog>(sp => sp.GetRequiredService<ServiceVersionRepository>());
        services.AddSingleton<EmbeddedServiceCatalog>();

        var workflows = configuration.GetConnectionString("Workflows")
                        ?? throw new InvalidOperationException("ConnectionStrings:Workflows is required.");
        var postgres = DatabaseProvider.IsPostgres(configuration);
        services.AddElsa(elsa =>
        {
            elsa.UseWorkflowManagement(management => management.UseEntityFrameworkCore(ef =>
            {
                if (postgres) ef.UsePostgreSql(workflows); else ef.UseSqlServer(workflows);
                ef.RunMigrations = true;
            }));
            elsa.UseWorkflowRuntime(runtime => runtime.UseEntityFrameworkCore(ef =>
            {
                if (postgres) ef.UsePostgreSql(workflows); else ef.UseSqlServer(workflows);
                ef.RunMigrations = true;
            }));
            elsa.AddActivitiesFrom<ApplicationProcessWorkflow>();
            elsa.AddWorkflow<ApplicationProcessWorkflow>();
            elsa.AddWorkflow<AgenticApplicationProcessWorkflow>();
        });
        services.AddScoped<IWorkflowEngine, ElsaWorkflowEngine>();

        // Common services, called service-to-service over the private network.
        var internalKey = configuration["Internal:ApiKey"] ?? "";
        services.AddHttpClient<IPaymentsGateway, PaymentsGateway>(c =>
        {
            c.BaseAddress = new Uri(configuration["Services:Payments"] ?? "http://payments.railway.internal:8080");
            c.DefaultRequestHeaders.Add(InternalApi.Header, internalKey);
        }).AddHttpMessageHandler<CorrelationIdHandler>();
        services.AddHttpClient<ICertificateIssuer, CertificateIssuer>(c =>
        {
            c.BaseAddress = new Uri(configuration["Services:Documents"] ?? "http://documents.railway.internal:8080");
            c.DefaultRequestHeaders.Add(InternalApi.Header, internalKey);
        }).AddHttpMessageHandler<CorrelationIdHandler>();

        services.AddHttpClient<IAiReviewer, AiReviewerClient>(c =>
        {
            c.BaseAddress = new Uri(configuration["Services:AiOrchestrator"] ?? "http://ai-orchestrator.railway.internal:8080");
            c.DefaultRequestHeaders.Add(InternalApi.Header, internalKey);
            c.Timeout = TimeSpan.FromSeconds(120);
        }).AddHttpMessageHandler<CorrelationIdHandler>();

        services.AddDspMessaging<EServicesDbContext>(configuration, "eservices");
        services.AddEventSubscription<PaymentCompleted, PaymentCompletedHandler>();
        return services;
    }

    /// <summary>Imports the shipped service definitions into an empty catalog (first start only).</summary>
    public static async Task SeedServiceCatalogAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<CatalogSeeder>();
        var shipped = scope.ServiceProvider.GetRequiredService<EmbeddedServiceCatalog>().GetAll();
        await seeder.SeedAsync(shipped, ct);
    }
}
