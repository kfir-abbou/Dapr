namespace ServiceA.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", GetHealth)
            .WithName("Health")
            .WithOpenApi();
    }

    private static IResult GetHealth()
    {
        return Results.Ok(new
        {
            Status = "Healthy",
            Service = "ServiceA",
            Timestamp = DateTime.UtcNow
        });
    }
}
