using Dapr;
using ServiceA.Models;

namespace ServiceA.Endpoints;

public static class EventSubscribers
{
    public static void MapEventSubscribers(this WebApplication app)
    {
        app.MapPost("/events/item-response", HandleItemResponse)
            .WithTopic(Constants.PubSubName, Constants.ItemResponseTopic)
            .WithName("HandleItemResponse")
            .WithOpenApi();

        app.MapPost("/events/workflow-progress", HandleWorkflowProgress)
            .WithTopic(Constants.PubSubName, Constants.WorkflowProgressTopic)
            .WithName("HandleWorkflowProgress")
            .WithOpenApi();

        app.MapPost("/events/batch-response", HandleBatchResponse)
            .WithTopic(Constants.PubSubName, Constants.BatchProcessResponseTopic)
            .WithName("HandleBatchResponse")
            .WithOpenApi();
    }

    private static IResult HandleItemResponse(ItemProcessResponse response, ILogger<Program> logger)
    {
        logger.LogInformation(
            "[PubSub] Received item response: CorrelationId={CorrelationId}, Success={Success}, Message={Message}",
            response.CorrelationId, response.Success, response.Message);

        return Results.Ok();
    }

    private static IResult HandleWorkflowProgress(WorkflowProgressEvent progress, ILogger<Program> logger)
    {
        logger.LogInformation(
            "[Workflow] Progress: {WorkflowId} - {ActivityName} - {PercentComplete}% - {Message}",
            progress.WorkflowInstanceId, progress.ActivityName, progress.PercentComplete, progress.Message);

        return Results.Ok();
    }

    private static IResult HandleBatchResponse(BatchProcessResponse response, ILogger<Program> logger)
    {
        logger.LogInformation(
            "[Batch] Received batch response: CorrelationId={CorrelationId}, Success={Success}, Duration={Duration}",
            response.CorrelationId, response.Success, response.TotalDuration);

        foreach (var result in response.WorkflowResults)
        {
            logger.LogInformation(
                "[Batch]   - {WorkflowName}: Success={Success}, Message={Message}",
                result.WorkflowName, result.Success, result.Message);
        }

        return Results.Ok();
    }
}
