using Dapr.Workflow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;
using ServiceB.Models;

namespace ServiceB.Endpoints;

public static class WorkflowEndpoints
{
    public static WebApplication MapWorkflowEndpoints(this WebApplication app)
    {
        // Get workflow status
        app.MapGet("/workflow/{instanceId}", async (string instanceId, DaprWorkflowClient workflowClient) =>
        {
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
                LastUpdatedAt = state.LastUpdatedAt
            });
        })
        .WithName("GetWorkflowStatus")
        .WithOpenApi();

        // Trigger approval for a waiting workflow
        app.MapPost("/workflow/approve", async (
            [FromBody] TriggerApprovalRequest request,
            DaprWorkflowClient workflowClient,
            ILogger<Program> logger) =>
        {
            try
            {
                // First check if the workflow instance exists
                var state = await workflowClient.GetWorkflowStateAsync(request.WorkflowInstanceId);
                if (state == null)
                {
                    logger.LogWarning(
                        "[Workflow] Approval failed - workflow instance '{InstanceId}' not found",
                        request.WorkflowInstanceId);
                    return Results.NotFound(new
                    {
                        Error = $"Workflow instance '{request.WorkflowInstanceId}' not found",
                        Hint = "Check the console output for the correct workflow instance ID (e.g., 'batch-xxxxx')"
                    });
                }

                // Check if workflow is still running
                if (state.RuntimeStatus.ToString() != OrchestrationRuntimeStatus.Running.ToString())
                {
                    logger.LogWarning(
                        "[Workflow] Approval failed - workflow instance '{InstanceId}' is not running (status: {Status})",
                        request.WorkflowInstanceId, state.RuntimeStatus);
                    return Results.BadRequest(new
                    {
                        Error = $"Workflow instance '{request.WorkflowInstanceId}' is not running",
                        Status = state.RuntimeStatus.ToString(),
                        Hint = "The workflow may have already completed or timed out"
                    });
                }

                var approvalEvent = new ApprovalEvent
                {
                    ApprovedBy = request.ApprovedBy,
                    IsApproved = request.IsApproved,
                    Comments = request.Comments,
                    ApprovedAt = DateTime.UtcNow
                };

                // Raise the external event to the waiting workflow
                await workflowClient.RaiseEventAsync(
                    request.WorkflowInstanceId,
                    Constants.ApprovalEventName,
                    approvalEvent);

                logger.LogInformation(
                    "[Workflow] Approval event sent to {InstanceId}: Approved={IsApproved} by {ApprovedBy}",
                    request.WorkflowInstanceId, request.IsApproved, request.ApprovedBy);

                return Results.Ok(new
                {
                    Message = $"Approval event sent to workflow '{request.WorkflowInstanceId}'",
                    IsApproved = request.IsApproved,
                    ApprovedBy = request.ApprovedBy
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send approval event to {InstanceId}", request.WorkflowInstanceId);
                return Results.Problem($"Failed to send approval: {ex.Message}");
            }
        })
        .WithName("TriggerApproval")
        .WithOpenApi();

        // List pending approval workflows (for debugging/monitoring)
        app.MapGet("/workflow/pending-approvals", () =>
        {
            // Note: In production you'd query a state store for pending workflows
            // This is a simplified example that returns usage instructions
            return Results.Ok(new
            {
                Message = "To approve a waiting workflow, call POST /workflow/approve",
                Note = "The workflow instance ID is the orchestrator instance ID shown in the console output when the workflow starts (e.g., 'batch-d2a57daf011e40df875f34d81a35a55f')",
                Example = new TriggerApprovalRequest
                {
                    WorkflowInstanceId = "batch-<your-instance-id>",
                    ApprovedBy = "admin",
                    IsApproved = true,
                    Comments = "Looks good!"
                }
            });
        })
        .WithName("GetPendingApprovals")
        .WithOpenApi();

        return app;
    }
}
