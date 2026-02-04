using Dapr;
using Dapr.Workflow;
using ServiceB.Models;

namespace ServiceB.Endpoints;

/// <summary>
/// Endpoints for subscribing to ServiceC events via Dapr pub/sub
/// </summary>
public static class ServiceCEventEndpoints
{
    public static void MapServiceCEventEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/servicec-events")
            .WithTags("ServiceC Events");

        // Subscribe to ServiceC progress events
        group.MapPost("/progress", HandleProgressEvent)
            .WithTopic(Constants.PubSubName, Constants.ServiceCProgressTopic)
            .WithName("ServiceCProgress");

        // Subscribe to ServiceC completion events
        group.MapPost("/complete", HandleCompleteEvent)
            .WithTopic(Constants.PubSubName, Constants.ServiceCCompleteTopic)
            .WithName("ServiceCComplete");
    }

    /// <summary>
    /// Handles progress events from ServiceC - just logs for visibility
    /// </summary>
    private static IResult HandleProgressEvent(ServiceCProgress progress)
    {
        Console.WriteLine($"[ServiceC-Progress] ========================================");
        Console.WriteLine($"[ServiceC-Progress] Workflow: {progress.WorkflowInstanceId}");
        Console.WriteLine($"[ServiceC-Progress] Step: [{progress.StepNumber}/{progress.TotalSteps}] {progress.StepName}");
        Console.WriteLine($"[ServiceC-Progress] Progress: {progress.PercentComplete}%");
        Console.WriteLine($"[ServiceC-Progress] ========================================");

        return Results.Ok(new { received = true, progress = progress.PercentComplete });
    }

    /// <summary>
    /// Handles completion events from ServiceC - raises external event to workflow
    /// </summary>
    private static async Task<IResult> HandleCompleteEvent(
        ServiceCComplete complete,
        DaprWorkflowClient workflowClient)
    {
        Console.WriteLine($"[ServiceC-Complete] ========================================");
        Console.WriteLine($"[ServiceC-Complete] Workflow: {complete.WorkflowInstanceId}");
        Console.WriteLine($"[ServiceC-Complete] Success: {complete.Success}");
        Console.WriteLine($"[ServiceC-Complete] Message: {complete.Message}");
        Console.WriteLine($"[ServiceC-Complete] ========================================");

        try
        {
            // Raise the external event to the waiting workflow
            await workflowClient.RaiseEventAsync(
                complete.WorkflowInstanceId,
                Constants.ServiceCCompleteEventName,
                complete);

            Console.WriteLine($"[ServiceC-Complete] Raised event '{Constants.ServiceCCompleteEventName}' to workflow '{complete.WorkflowInstanceId}'");

            return Results.Ok(new 
            { 
                received = true, 
                eventRaised = true,
                workflowInstanceId = complete.WorkflowInstanceId 
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServiceC-Complete] ERROR: Failed to raise event: {ex.Message}");
            
            return Results.Problem(
                detail: $"Failed to raise event: {ex.Message}",
                statusCode: 500);
        }
    }
}
