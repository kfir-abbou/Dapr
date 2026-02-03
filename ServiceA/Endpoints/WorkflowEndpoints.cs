using Dapr.Workflow;

namespace ServiceA.Endpoints;

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        app.MapGet("/workflow/{instanceId}", GetWorkflowStatus)
            .WithName("GetWorkflowStatus")
            .WithOpenApi();
    }

    private static async Task<IResult> GetWorkflowStatus(DaprWorkflowClient workflowClient, string instanceId)
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
            LastUpdatedAt = state.LastUpdatedAt,
            Output = state.ReadOutputAs<string>()
        });
    }
}
