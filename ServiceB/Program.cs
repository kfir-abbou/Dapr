using ServiceB.Configuration;
using ServiceB.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Get instance ID for logging
var instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID") ?? $"ServiceB-{Environment.ProcessId}";

// Configure all application services
builder.Services.AddApplicationServices();

var app = builder.Build();

// Log instance startup
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"");
    Console.WriteLine($"╔══════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║  {instanceId,-54}  ║");
    Console.WriteLine($"║  Listening on: {app.Urls.FirstOrDefault() ?? "http://localhost:5002",-40}  ║");
    Console.WriteLine($"╚══════════════════════════════════════════════════════════╝");
    Console.WriteLine($"");
});

// Configure middleware
app.ConfigureMiddleware();

// Map all endpoints
app.MapItemEndpoints();
app.MapHealthEndpoints();
app.MapEventSubscribers();
app.MapWorkflowEndpoints();
app.MapBatchProcessEndpoints();
app.MapServiceCEventEndpoints();

app.Run();
