using Dapr;
using Dapr.Client;
using Dapr.Workflow;
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

// Add Dapr Workflow
builder.Services.AddDaprWorkflow(options =>
{
    // Register the setup workflow and its activities
    options.RegisterWorkflow<SetupWorkflow>();
    options.RegisterActivity<InitializeSystemActivity>();
    options.RegisterActivity<ConfigureParametersActivity>();
    options.RegisterActivity<ValidateConfigurationActivity>();
    options.RegisterActivity<FinalizeSetupActivity>();
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
const string ItemRequestTopic = "item-requests";
const string ItemResponseTopic = "item-responses";
const string WorkflowProgressTopic = "workflow-progress";

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

    // Start setup workflow if transitioning to "setup" state
    string? workflowInstanceId = null;
    if (requestedState == "setup")
    {
        var workflowClient = app.Services.GetRequiredService<DaprWorkflowClient>();
        workflowInstanceId = $"setup-{Guid.NewGuid():N}";
        await workflowClient.ScheduleNewWorkflowAsync(
            name: nameof(SetupWorkflow),
            instanceId: workflowInstanceId,
            input: new SetupWorkflowInput { WorkflowInstanceId = workflowInstanceId, StartedAt = DateTime.UtcNow });
        Console.WriteLine($"[Workflow] Started SetupWorkflow with instance ID: {workflowInstanceId}");
    }

    return Results.Ok(new StateChangeResult
    {
        Success = true,
        Message = $"State changed from '{currentStatus}' to '{requestedState}'" + 
                  (workflowInstanceId != null ? $". Workflow started: {workflowInstanceId}" : ""),
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

// ==================== Async Pub/Sub Endpoints ====================

// Request item processing asynchronously via Dapr Pub/Sub
app.MapPost("/items/process", async (DaprClient daprClient, [FromBody] ItemProcessRequest request) =>
{
    var correlationId = Guid.NewGuid().ToString();
    
    var message = new ItemProcessMessage
    {
        CorrelationId = correlationId,
        ItemId = request.ItemId,
        Operation = request.Operation,
        RequestedBy = "ServiceA",
        RequestedAt = DateTime.UtcNow
    };
    
    // Publish request to ServiceB via Dapr Pub/Sub
    await daprClient.PublishEventAsync(PubSubName, ItemRequestTopic, message);
    
    Console.WriteLine($"[PubSub] Published item process request: {correlationId} - Operation: {request.Operation} on Item {request.ItemId}");
    
    return Results.Accepted(value: new
    {
        Message = "Item processing request submitted",
        CorrelationId = correlationId,
        Operation = request.Operation,
        ItemId = request.ItemId
    });
})
.WithName("RequestItemProcessing")
.WithOpenApi();

// Subscribe to item processing responses from ServiceB
app.MapPost("/events/item-response", [Topic(PubSubName, ItemResponseTopic)] (ItemProcessResponse response, ILogger<Program> logger) =>
{
    logger.LogInformation(
        "[PubSub] Received item response: CorrelationId={CorrelationId}, Success={Success}, Message={Message}",
        response.CorrelationId, response.Success, response.Message);
    
    // Here you could:
    // - Store the result in state store
    // - Notify connected clients via SignalR
    // - Trigger another workflow
    // - Update a dashboard
    
    return Results.Ok();
})
.WithName("HandleItemResponse")
.WithOpenApi();

// Subscribe to workflow progress events
app.MapPost("/events/workflow-progress", [Topic(PubSubName, WorkflowProgressTopic)] (WorkflowProgressEvent progress, ILogger<Program> logger) =>
{
    logger.LogInformation(
        "[Workflow] Progress: {WorkflowId} - {ActivityName} - {PercentComplete}% - {Message}",
        progress.WorkflowInstanceId, progress.ActivityName, progress.PercentComplete, progress.Message);
    
    // Here you could:
    // - Update a progress indicator in the UI
    // - Store progress in state store for polling
    // - Send real-time updates via SignalR
    
    return Results.Ok();
})
.WithName("HandleWorkflowProgress")
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

// ==================== Workflow Endpoints ====================

// Get workflow status
app.MapGet("/workflow/{instanceId}", async (string instanceId) =>
{
    var workflowClient = app.Services.GetRequiredService<DaprWorkflowClient>();
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
    {
        return Results.NotFound($"Workflow instance '{instanceId}' not found");
    }
    
    return Results.Ok(new
    {
        InstanceId = instanceId,
        Status = state.RuntimeStatus.ToString(),
        CreatedAt = state.CreatedAt,
        LastUpdatedAt = state.LastUpdatedAt,
        Output = state.ReadOutputAs<string>()
    });
})
.WithName("GetWorkflowStatus")
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

// ==================== Pub/Sub Models ====================

public record ItemProcessRequest
{
    public int ItemId { get; init; }
    public string Operation { get; init; } = string.Empty; // "validate", "enrich", "archive"
}

public record ItemProcessMessage
{
    public string CorrelationId { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string Operation { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}

public record ItemProcessResponse
{
    public string CorrelationId { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string Operation { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime ProcessedAt { get; init; }
}

// ==================== Workflow Models ====================

public record SetupWorkflowInput
{
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
}

public record WorkflowProgressEvent
{
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = string.Empty;
    public string ActivityName { get; init; } = string.Empty;
    public int PercentComplete { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public record ActivityInput
{
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int StepNumber { get; init; }
    public int TotalSteps { get; init; }
}

public record ActivityResult
{
    public string ActivityName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime CompletedAt { get; init; }
}

// ==================== Setup Workflow ====================

public class SetupWorkflow : Workflow<SetupWorkflowInput, string>
{
    private const int TotalSteps = 4;
    
    public override async Task<string> RunAsync(WorkflowContext context, SetupWorkflowInput input)
    {
        var results = new List<ActivityResult>();
        
        // Step 1: Initialize System (25%)
        var initResult = await context.CallActivityAsync<ActivityResult>(
            nameof(InitializeSystemActivity), 
            new ActivityInput 
            { 
                WorkflowInstanceId = input.WorkflowInstanceId,
                Description = "Starting system initialization",
                StepNumber = 1,
                TotalSteps = TotalSteps
            });
        results.Add(initResult);
        
        // Step 2: Configure Parameters (50%)
        var configResult = await context.CallActivityAsync<ActivityResult>(
            nameof(ConfigureParametersActivity), 
            new ActivityInput 
            { 
                WorkflowInstanceId = input.WorkflowInstanceId,
                Description = "Configuring system parameters",
                StepNumber = 2,
                TotalSteps = TotalSteps
            });
        results.Add(configResult);
        
        // Step 3: Validate Configuration (75%)
        var validateResult = await context.CallActivityAsync<ActivityResult>(
            nameof(ValidateConfigurationActivity), 
            new ActivityInput 
            { 
                WorkflowInstanceId = input.WorkflowInstanceId,
                Description = "Validating configuration",
                StepNumber = 3,
                TotalSteps = TotalSteps
            });
        results.Add(validateResult);
        
        // Step 4: Finalize Setup (100%)
        var finalizeResult = await context.CallActivityAsync<ActivityResult>(
            nameof(FinalizeSetupActivity), 
            new ActivityInput 
            { 
                WorkflowInstanceId = input.WorkflowInstanceId,
                Description = "Finalizing setup",
                StepNumber = 4,
                TotalSteps = TotalSteps
            });
        results.Add(finalizeResult);
        
        return $"Setup completed successfully. {results.Count} activities executed.";
    }
}

// ==================== Workflow Activities ====================

public class InitializeSystemActivity : WorkflowActivity<ActivityInput, ActivityResult>
{
    private readonly DaprClient _daprClient;
    private const string PubSubName = "pubsub";
    private const string ProgressTopic = "workflow-progress";
    
    public InitializeSystemActivity(DaprClient daprClient)
    {
        _daprClient = daprClient;
    }
    
    public override async Task<ActivityResult> RunAsync(WorkflowActivityContext context, ActivityInput input)
    {
        var percentComplete = (int)((double)input.StepNumber / input.TotalSteps * 100);
        
        // Publish progress: starting
        await _daprClient.PublishEventAsync(PubSubName, ProgressTopic, new WorkflowProgressEvent
        {
            WorkflowInstanceId = input.WorkflowInstanceId,
            WorkflowName = nameof(SetupWorkflow),
            ActivityName = nameof(InitializeSystemActivity),
            PercentComplete = percentComplete - 25 + 5, // Starting this step
            Message = $"Starting: {input.Description}",
            Timestamp = DateTime.UtcNow
        });
        
        Console.WriteLine($"[Activity] InitializeSystemActivity: {input.Description}");
        
        // Simulate work
        await Task.Delay(3000);
        
        // Publish progress: completed
        await _daprClient.PublishEventAsync(PubSubName, ProgressTopic, new WorkflowProgressEvent
        {
            WorkflowInstanceId = input.WorkflowInstanceId,
            WorkflowName = nameof(SetupWorkflow),
            ActivityName = nameof(InitializeSystemActivity),
            PercentComplete = percentComplete,
            Message = "System initialized successfully",
            Timestamp = DateTime.UtcNow
        });
        
        return new ActivityResult
        {
            ActivityName = nameof(InitializeSystemActivity),
            Success = true,
            Message = "System initialized successfully",
            CompletedAt = DateTime.UtcNow
        };
    }
}

public class ConfigureParametersActivity : WorkflowActivity<ActivityInput, ActivityResult>
{
    private readonly DaprClient _daprClient;
    private const string PubSubName = "pubsub";
    private const string ProgressTopic = "workflow-progress";
    
    public ConfigureParametersActivity(DaprClient daprClient)
    {
        _daprClient = daprClient;
    }
    
    public override async Task<ActivityResult> RunAsync(WorkflowActivityContext context, ActivityInput input)
    {
        var percentComplete = (int)((double)input.StepNumber / input.TotalSteps * 100);
        
        // Publish progress: starting
        await _daprClient.PublishEventAsync(PubSubName, ProgressTopic, new WorkflowProgressEvent
        {
            WorkflowInstanceId = input.WorkflowInstanceId,
            WorkflowName = nameof(SetupWorkflow),
            ActivityName = nameof(ConfigureParametersActivity),
            PercentComplete = percentComplete - 25 + 5,
            Message = $"Starting: {input.Description}",
            Timestamp = DateTime.UtcNow
        });
        
        Console.WriteLine($"[Activity] ConfigureParametersActivity: {input.Description}");
        
        // Simulate work
        await Task.Delay(2000);
        
        // Publish progress: completed
        await _daprClient.PublishEventAsync(PubSubName, ProgressTopic, new WorkflowProgressEvent
        {
            WorkflowInstanceId = input.WorkflowInstanceId,
            WorkflowName = nameof(SetupWorkflow),
            ActivityName = nameof(ConfigureParametersActivity),
            PercentComplete = percentComplete,
            Message = "Parameters configured successfully",
            Timestamp = DateTime.UtcNow
        });
        
        return new ActivityResult
        {
            ActivityName = nameof(ConfigureParametersActivity),
            Success = true,
            Message = "Parameters configured successfully",
            CompletedAt = DateTime.UtcNow
        };
    }
}

public class ValidateConfigurationActivity : WorkflowActivity<ActivityInput, ActivityResult>
{
    private readonly DaprClient _daprClient;
    private const string PubSubName = "pubsub";
    private const string ProgressTopic = "workflow-progress";
    
    public ValidateConfigurationActivity(DaprClient daprClient)
    {
        _daprClient = daprClient;
    }
    
    public override async Task<ActivityResult> RunAsync(WorkflowActivityContext context, ActivityInput input)
    {
        var percentComplete = (int)((double)input.StepNumber / input.TotalSteps * 100);
        
        // Publish progress: starting
        await _daprClient.PublishEventAsync(PubSubName, ProgressTopic, new WorkflowProgressEvent
        {
            WorkflowInstanceId = input.WorkflowInstanceId,
            WorkflowName = nameof(SetupWorkflow),
            ActivityName = nameof(ValidateConfigurationActivity),
            PercentComplete = percentComplete - 25 + 5,
            Message = $"Starting: {input.Description}",
            Timestamp = DateTime.UtcNow
        });
        
        Console.WriteLine($"[Activity] ValidateConfigurationActivity: {input.Description}");
        
        // Simulate work
        await Task.Delay(2500);
        
        // Publish progress: completed
        await _daprClient.PublishEventAsync(PubSubName, ProgressTopic, new WorkflowProgressEvent
        {
            WorkflowInstanceId = input.WorkflowInstanceId,
            WorkflowName = nameof(SetupWorkflow),
            ActivityName = nameof(ValidateConfigurationActivity),
            PercentComplete = percentComplete,
            Message = "Configuration validated successfully",
            Timestamp = DateTime.UtcNow
        });
        
        return new ActivityResult
        {
            ActivityName = nameof(ValidateConfigurationActivity),
            Success = true,
            Message = "Configuration validated successfully",
            CompletedAt = DateTime.UtcNow
        };
    }
}

public class FinalizeSetupActivity : WorkflowActivity<ActivityInput, ActivityResult>
{
    private readonly DaprClient _daprClient;
    private const string PubSubName = "pubsub";
    private const string ProgressTopic = "workflow-progress";
    
    public FinalizeSetupActivity(DaprClient daprClient)
    {
        _daprClient = daprClient;
    }
    
    public override async Task<ActivityResult> RunAsync(WorkflowActivityContext context, ActivityInput input)
    {
        var percentComplete = (int)((double)input.StepNumber / input.TotalSteps * 100);
        
        // Publish progress: starting
        await _daprClient.PublishEventAsync(PubSubName, ProgressTopic, new WorkflowProgressEvent
        {
            WorkflowInstanceId = input.WorkflowInstanceId,
            WorkflowName = nameof(SetupWorkflow),
            ActivityName = nameof(FinalizeSetupActivity),
            PercentComplete = percentComplete - 25 + 5,
            Message = $"Starting: {input.Description}",
            Timestamp = DateTime.UtcNow
        });
        
        Console.WriteLine($"[Activity] FinalizeSetupActivity: {input.Description}");
        
        // Simulate work
        await Task.Delay(1500);
        
        // Publish progress: completed
        await _daprClient.PublishEventAsync(PubSubName, ProgressTopic, new WorkflowProgressEvent
        {
            WorkflowInstanceId = input.WorkflowInstanceId,
            WorkflowName = nameof(SetupWorkflow),
            ActivityName = nameof(FinalizeSetupActivity),
            PercentComplete = percentComplete,
            Message = "Setup finalized successfully - Workflow complete!",
            Timestamp = DateTime.UtcNow
        });
        
        return new ActivityResult
        {
            ActivityName = nameof(FinalizeSetupActivity),
            Success = true,
            Message = "Setup finalized successfully",
            CompletedAt = DateTime.UtcNow
        };
    }
}
