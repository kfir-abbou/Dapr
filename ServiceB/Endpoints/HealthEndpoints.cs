namespace ServiceB.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new 
        { 
            Status = "Healthy", 
            Service = "ServiceB", 
            Timestamp = DateTime.UtcNow 
        }))
        .WithName("Health")
        .WithOpenApi();

        return app;
    }
}
