using Dapr.Client;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add Dapr client with configuration from environment variables
builder.Services.AddDaprClient(daprClientBuilder =>
{
    // Dapr sidecar sets DAPR_HTTP_PORT and DAPR_GRPC_PORT environment variables
    var daprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500";
    var daprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "50001";
    
    daprClientBuilder.UseHttpEndpoint($"http://localhost:{daprHttpPort}");
    daprClientBuilder.UseGrpcEndpoint($"http://localhost:{daprGrpcPort}");
});

// Add HttpClient for ServiceB (direct calls for local development)
builder.Services.AddHttpClient("ServiceB", client =>
{
    client.BaseAddress = new Uri("http://localhost:5002");
});

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable Dapr pub/sub
app.UseCloudEvents();
app.MapSubscribeHandler();

// Constants
const string StateStoreName = "statestore";
const string SystemStateKey = "system-state";
const string PubSubName = "pubsub";
const string TopicName = "system-events";

// System state options
var validStates = new[] { "idle", "running", "setup", "procedure", "error" };

// ==================== System State Endpoints ====================

// Get current system state
app.MapGet("/state", async (DaprClient daprClient) =>
{
    var state = await daprClient.GetStateAsync<SystemState>(StateStoreName, SystemStateKey);
    return state ?? new SystemState { Status = "idle", LastUpdated = DateTime.UtcNow };
})
.WithName("GetSystemState")
.WithOpenApi();

// Set system state
app.MapPost("/state", async (DaprClient daprClient, [FromBody] SetStateRequest request) =>
{
    if (!validStates.Contains(request.Status.ToLower()))
    {
        return Results.BadRequest($"Invalid state. Valid states are: {string.Join(", ", validStates)}");
    }

    var newState = new SystemState
    {
        Status = request.Status.ToLower(),
        LastUpdated = DateTime.UtcNow
    };

    // Save state using Dapr state store
    await daprClient.SaveStateAsync(StateStoreName, SystemStateKey, newState);

    // Publish state change event
    await daprClient.PublishEventAsync(PubSubName, TopicName, new SystemEvent
    {
        EventType = "StateChanged",
        NewState = newState.Status,
        Timestamp = DateTime.UtcNow
    });

    return Results.Ok(newState);
})
.WithName("SetSystemState")
.WithOpenApi();

// ==================== Service B Integration Endpoints ====================

// Get all items from ServiceB via direct HTTP call
app.MapGet("/items", async (IHttpClientFactory httpClientFactory) =>
{
    try
    {
        var client = httpClientFactory.CreateClient("ServiceB");
        var items = await client.GetFromJsonAsync<List<Item>>("items");
        return Results.Ok(items);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error calling ServiceB: {ex.Message}");
    }
})
.WithName("GetItems")
.WithOpenApi();

// Get specific item from ServiceB via direct HTTP call
app.MapGet("/items/{id:int}", async (IHttpClientFactory httpClientFactory, int id) =>
{
    try
    {
        var client = httpClientFactory.CreateClient("ServiceB");
        var response = await client.GetAsync($"items/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Results.NotFound($"Item {id} not found");
        }
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<Item>();
        return Results.Ok(item);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error calling ServiceB: {ex.Message}");
    }
})
.WithName("GetItemById")
.WithOpenApi();

// ==================== Health Endpoint ====================

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ServiceA", Timestamp = DateTime.UtcNow }))
.WithName("Health")
.WithOpenApi();

app.Run();

// ==================== Models ====================

public record SystemState
{
    public string Status { get; init; } = "idle";
    public DateTime LastUpdated { get; init; }
}

public record SetStateRequest
{
    public string Status { get; init; } = string.Empty;
}

public record SystemEvent
{
    public string EventType { get; init; } = string.Empty;
    public string NewState { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public record Item
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
}
