using Dapr.Workflow;
using ServiceA.Activities;
using ServiceA.Services;
using ServiceA.Workflows;

namespace ServiceA.Configuration;

public static class ServiceConfiguration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Dapr client
        services.AddDaprClient(daprClientBuilder =>
        {
            var daprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500";
            var daprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "50001";

            daprClientBuilder.UseHttpEndpoint($"http://localhost:{daprHttpPort}");
            daprClientBuilder.UseGrpcEndpoint($"http://localhost:{daprGrpcPort}");
        });

        // Add Dapr Workflow
        services.AddDaprWorkflow(options =>
        {
            options.RegisterWorkflow<SetupWorkflow>();
            options.RegisterActivity<InitializeSystemActivity>();
            options.RegisterActivity<ConfigureParametersActivity>();
            options.RegisterActivity<ValidateConfigurationActivity>();
            options.RegisterActivity<FinalizeSetupActivity>();
        });

        // Add HttpClient for ServiceB
        services.AddHttpClient("ServiceB", client =>
        {
            client.BaseAddress = new Uri("http://localhost:5002");
        });

        // Add application services
        services.AddScoped<StateBackupService>();

        // Add API services
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }

    public static WebApplication ConfigureMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCloudEvents();
        app.MapSubscribeHandler();

        return app;
    }
}
