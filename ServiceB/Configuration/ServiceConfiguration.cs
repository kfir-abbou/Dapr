using Dapr.Workflow;
using ServiceB.Stores;
using ServiceB.Workflows;
using ServiceB.Activities;

namespace ServiceB.Configuration;

public static class ServiceConfiguration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add Dapr client with configuration from environment variables
        services.AddDaprClient(daprClientBuilder =>
        {
            var daprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500";
            var daprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "50002";
            
            daprClientBuilder.UseHttpEndpoint($"http://localhost:{daprHttpPort}");
            daprClientBuilder.UseGrpcEndpoint($"http://localhost:{daprGrpcPort}");
        });

        // Add Dapr Workflow
        services.AddDaprWorkflow(options =>
        {
            // Register orchestrator workflow
            options.RegisterWorkflow<BatchOrchestratorWorkflow>();
            
            // Register activities
            options.RegisterActivity<DelayActivity>();
            options.RegisterActivity<SendToServiceCActivity>();
        });

        // Add API documentation
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Add in-memory data store
        services.AddSingleton<ItemStore>();

        return services;
    }

    public static WebApplication ConfigureMiddleware(this WebApplication app)
    {
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Enable Dapr pub/sub
        app.UseCloudEvents();
        app.MapSubscribeHandler();

        return app;
    }
}
