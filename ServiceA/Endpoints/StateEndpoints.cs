using Dapr.Client;
using Dapr.Workflow;
using Microsoft.AspNetCore.Mvc;
using ServiceA.Models;
using ServiceA.Services;
using ServiceA.Workflows;

namespace ServiceA.Endpoints;

public static class StateEndpoints
{
    public static void MapStateEndpoints(this WebApplication app)
    {
        app.MapGet("/state", GetSystemState)
            .WithName("GetSystemState")
            .WithOpenApi();

        app.MapGet("/state/transitions", GetStateTransitions)
            .WithName("GetStateTransitions")
            .WithOpenApi();

        app.MapPost("/state", ChangeSystemState)
            .WithName("ChangeSystemState")
            .WithOpenApi();

        app.MapPost("/state/force", ForceSystemState)
            .WithName("ForceSystemState")
            .WithOpenApi();
    }

    private static async Task<SystemState> GetSystemState(DaprClient daprClient)
    {
        var state = await daprClient.GetStateAsync<SystemState>(Constants.StateStoreName, Constants.SystemStateKey);
        return state ?? new SystemState { Status = "idle", LastUpdated = DateTime.UtcNow };
    }

    private static async Task<IResult> GetStateTransitions(DaprClient daprClient)
    {
        var state = await daprClient.GetStateAsync<SystemState>(Constants.StateStoreName, Constants.SystemStateKey);
        var currentStatus = state?.Status ?? "idle";

        var allowedTransitions = Constants.ValidTransitions.GetValueOrDefault(currentStatus, Array.Empty<string>());

        return Results.Ok(new
        {
            CurrentState = currentStatus,
            AllowedTransitions = allowedTransitions
        });
    }

    private static async Task<IResult> ChangeSystemState(
        DaprClient daprClient,
        StateBackupService stateBackupService,
        DaprWorkflowClient workflowClient,
        [FromBody] SetStateRequest request)
    {
        var requestedState = request.Status.ToLower();

        if (!Constants.ValidStates.Contains(requestedState))
        {
            return Results.BadRequest(new StateChangeResult
            {
                Success = false,
                Message = $"Invalid state '{request.Status}'. Valid states are: {string.Join(", ", Constants.ValidStates)}",
                CurrentState = null,
                RequestedState = requestedState
            });
        }

        var currentState = await daprClient.GetStateAsync<SystemState>(Constants.StateStoreName, Constants.SystemStateKey);
        var currentStatus = currentState?.Status ?? "idle";

        var allowedTransitions = Constants.ValidTransitions.GetValueOrDefault(currentStatus, Array.Empty<string>());

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

        var newState = new SystemState
        {
            Status = requestedState,
            LastUpdated = DateTime.UtcNow,
            PreviousStatus = currentStatus
        };

        await daprClient.SaveStateAsync(Constants.StateStoreName, Constants.SystemStateKey, newState);
        await stateBackupService.SaveStateToFileAsync(newState);

        await daprClient.PublishEventAsync(Constants.PubSubName, Constants.TopicName, new SystemEvent
        {
            EventType = "StateChanged",
            PreviousState = currentStatus,
            NewState = newState.Status,
            Timestamp = DateTime.UtcNow
        });

        string? workflowInstanceId = null;
        if (requestedState == "setup")
        {
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
    }

    private static async Task<IResult> ForceSystemState(
        DaprClient daprClient,
        StateBackupService stateBackupService,
        [FromBody] SetStateRequest request)
    {
        var requestedState = request.Status.ToLower();

        if (!Constants.ValidStates.Contains(requestedState))
        {
            return Results.BadRequest($"Invalid state. Valid states are: {string.Join(", ", Constants.ValidStates)}");
        }

        var currentState = await daprClient.GetStateAsync<SystemState>(Constants.StateStoreName, Constants.SystemStateKey);
        var currentStatus = currentState?.Status ?? "idle";

        var newState = new SystemState
        {
            Status = requestedState,
            LastUpdated = DateTime.UtcNow,
            PreviousStatus = currentStatus
        };

        await daprClient.SaveStateAsync(Constants.StateStoreName, Constants.SystemStateKey, newState);
        await stateBackupService.SaveStateToFileAsync(newState);

        await daprClient.PublishEventAsync(Constants.PubSubName, Constants.TopicName, new SystemEvent
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
    }
}
