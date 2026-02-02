using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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

// State backup configuration from appsettings
var stateBackupBindingName = builder.Configuration["StateBackup:BindingName"] ?? "statebackup";
var stateBackupFileName = builder.Configuration["StateBackup:FileName"] ?? "system-state.json";

// System state options
var validStates = new[] { "idle", "running", "setup", "procedure", "error" };

// State transition rules - defines which states can transition to which
var validTransitions = new Dictionary<string, string[]>
{
    { "idle", new[] { "setup", "error" } },
    { "setup", new[] { "running", "idle", "error" } },
    { "running", new[] { "procedure", "idle", "error" } },
    { "procedure", new[] { "running", "idle", "error" } },
    { "error", new[] { "idle" } }  // From error, can only go back to idle
};

// Helper function to save state to file using Dapr binding
async Task SaveStateToFileAsync(DaprClient daprClient, SystemState state)
{
    var stateJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    var metadata = new Dictionary<string, string>
    {
        { "fileName", stateBackupFileName }
    };
    
    await daprClient.InvokeBindingAsync(
        stateBackupBindingName,
        "create",
        stateJson,
        metadata);
}

// Save initial state to file on app startup
app.Lifetime.ApplicationStarted.Register(async () =>
{
    // Wait a moment for Dapr sidecar to be ready
    await Task.Delay(2000);
    
    try
    {
        using var scope = app.Services.CreateScope();
        var daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();
        
        var state = await daprClient.GetStateAsync<SystemState>(StateStoreName, SystemStateKey);
        var currentState = state ?? new SystemState { Status = "idle", LastUpdated = DateTime.UtcNow };
        
        // If no state exists, save initial state
        if (state == null)
        {
            await daprClient.SaveStateAsync(StateStoreName, SystemStateKey, currentState);
        }
        
        // Save state to file
        await SaveStateToFileAsync(daprClient, currentState);
        Console.WriteLine($"[StateBackup] Initial state saved to file: {stateBackupFileName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[StateBackup] Error saving initial state: {ex.Message}");
    }
});

// ==================== System State Endpoints ====================

// Get current system state
app.MapGet("/state", async (DaprClient daprClient) =>
{
    var state = await daprClient.GetStateAsync<SystemState>(StateStoreName, SystemStateKey);
    return state ?? new SystemState { Status = "idle", LastUpdated = DateTime.UtcNow };
})
.WithName("GetSystemState")
.WithOpenApi();

// Get valid transitions from current state
app.MapGet("/state/transitions", async (DaprClient daprClient) =>
{
    var state = await daprClient.GetStateAsync<SystemState>(StateStoreName, SystemStateKey);
    var currentStatus = state?.Status ?? "idle";
    
    var allowedTransitions = validTransitions.GetValueOrDefault(currentStatus, Array.Empty<string>());
    
    return Results.Ok(new
    {
        CurrentState = currentStatus,
        AllowedTransitions = allowedTransitions
    });
})
.WithName("GetStateTransitions")
.WithOpenApi();

// Change system state (with validation)
app.MapPost("/state", async (DaprClient daprClient, [FromBody] SetStateRequest request) =>
{
    var requestedState = request.Status.ToLower();
    
    // Validate the requested state is a known state
    if (!validStates.Contains(requestedState))
    {
        return Results.BadRequest(new StateChangeResult
        {
            Success = false,
            Message = $"Invalid state '{request.Status}'. Valid states are: {string.Join(", ", validStates)}",
            CurrentState = null,
            RequestedState = requestedState
        });
    }

    // Get current state
    var currentState = await daprClient.GetStateAsync<SystemState>(StateStoreName, SystemStateKey);
    var currentStatus = currentState?.Status ?? "idle";

    // Check if transition is valid
    var allowedTransitions = validTransitions.GetValueOrDefault(currentStatus, Array.Empty<string>());
    
    if (currentStatus == requestedState)
    {
        return Results.Ok(new StateChangeResult
        {
            Success = true,
            Message = $"Already in state '{currentStatus}'",
            CurrentState = currentStatus,
            RequestedState = requestedState
        });
    }

    if (!allowedTransitions.Contains(requestedState))
    {
        return Results.BadRequest(new StateChangeResult
        {
            Success = false,
            Message = $"Invalid transition from '{currentStatus}' to '{requestedState}'. Allowed transitions: {string.Join(", ", allowedTransitions)}",
            CurrentState = currentStatus,
            RequestedState = requestedState
        });
    }

    // Create new state
    var newState = new SystemState
    {
        Status = requestedState,
        LastUpdated = DateTime.UtcNow,
        PreviousStatus = currentStatus
    };

    // Save state using Dapr state store
    await daprClient.SaveStateAsync(StateStoreName, SystemStateKey, newState);

    // Save state to file using Dapr binding
    await SaveStateToFileAsync(daprClient, newState);

    // Publish state change event
    await daprClient.PublishEventAsync(PubSubName, TopicName, new SystemEvent
    {
        EventType = "StateChanged",
        PreviousState = currentStatus,
        NewState = newState.Status,
        Timestamp = DateTime.UtcNow
    });

    return Results.Ok(new StateChangeResult
    {
        Success = true,
        Message = $"State changed from '{currentStatus}' to '{requestedState}'",
        CurrentState = requestedState,
        RequestedState = requestedState,
        PreviousState = currentStatus
    });
})
.WithName("ChangeSystemState")
.WithOpenApi();

// Force set state (bypasses transition validation - for admin/recovery)
app.MapPost("/state/force", async (DaprClient daprClient, [FromBody] SetStateRequest request) =>
{
    var requestedState = request.Status.ToLower();
    
    if (!validStates.Contains(requestedState))
    {
        return Results.BadRequest($"Invalid state. Valid states are: {string.Join(", ", validStates)}");
    }

    var currentState = await daprClient.GetStateAsync<SystemState>(StateStoreName, SystemStateKey);
    var currentStatus = currentState?.Status ?? "idle";

    var newState = new SystemState
    {
        Status = requestedState,
        LastUpdated = DateTime.UtcNow,
        PreviousStatus = currentStatus
    };

    await daprClient.SaveStateAsync(StateStoreName, SystemStateKey, newState);

    // Save state to file using Dapr binding
    await SaveStateToFileAsync(daprClient, newState);

    await daprClient.PublishEventAsync(PubSubName, TopicName, new SystemEvent
    {
        EventType = "StateForced",
        PreviousState = currentStatus,
        NewState = newState.Status,
        Timestamp = DateTime.UtcNow
    });

    return Results.Ok(new StateChangeResult
    {
        Success = true,
        Message = $"State forced from '{currentStatus}' to '{requestedState}'",
        CurrentState = requestedState,
        RequestedState = requestedState,
        PreviousState = currentStatus
    });
})
.WithName("ForceSystemState")
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
    public string? PreviousStatus { get; init; }
}

public record SetStateRequest
{
    public string Status { get; init; } = string.Empty;
}

public record StateChangeResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? CurrentState { get; init; }
    public string? RequestedState { get; init; }
    public string? PreviousState { get; init; }
}

public record SystemEvent
{
    public string EventType { get; init; } = string.Empty;
    public string? PreviousState { get; init; }
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
