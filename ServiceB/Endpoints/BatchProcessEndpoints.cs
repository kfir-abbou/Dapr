using Dapr;
using Dapr.Client;
using Dapr.Workflow;
using ServiceB.Models;

namespace ServiceB.Endpoints;

public static class BatchProcessEndpoints
{
    // Get instance ID from environment variable (set in dapr.yaml)
    private static readonly string InstanceId = Environment.GetEnvironmentVariable("INSTANCE_ID") ?? $"ServiceB-{Environment.ProcessId}";
    
    public static WebApplication MapBatchProcessEndpoints(this WebApplication app)
    {
        // Subscribe to batch processing requests from Service A
        app.MapPost("/events/batch-process", [Topic(Constants.PubSubName, Constants.BatchProcessRequestTopic)]
            async (BatchProcessRequest request, DaprWorkflowClient workflowClient, DaprClient daprClient, ILogger<Program> logger) =>
        {
            Console.WriteLine($"[BatchProcess] >>>>>>>>>> RECEIVED BATCH REQUEST <<<<<<<<<<");
            Console.WriteLine($"[BatchProcess] Instance: {InstanceId}");
            Console.WriteLine($"[BatchProcess] CorrelationId: {request.CorrelationId}");
            Console.WriteLine($"[BatchProcess] RequestedBy: {request.RequestedBy}");
            
            logger.LogInformation(
                "[{Instance}] Received batch process request: CorrelationId={CorrelationId} from {RequestedBy}",
                InstanceId, request.CorrelationId, request.RequestedBy);

            var startTime = DateTime.UtcNow;
            var orchestratorInstanceId = $"batch-{request.CorrelationId}";

            try
            {
                // Start the orchestrator workflow that will run all child workflows in parallel
                await workflowClient.ScheduleNewWorkflowAsync(
                    nameof(Workflows.BatchOrchestratorWorkflow),
                    orchestratorInstanceId,
                    new BatchOrchestratorInput
                    {
                        CorrelationId = request.CorrelationId,
                        RequestedBy = request.RequestedBy,
                        StartedAt = startTime
                    });

                logger.LogInformation(
                    "[Workflow] Started BatchOrchestratorWorkflow: {InstanceId}. Approval workflow ID: approval-{InstanceId}",
                    orchestratorInstanceId, orchestratorInstanceId);

                // Wait for the orchestrator to complete (this will block until all child workflows finish)
                var state = await workflowClient.WaitForWorkflowCompletionAsync(orchestratorInstanceId);
                
                var endTime = DateTime.UtcNow;
                var output = state?.ReadOutputAs<BatchOrchestratorOutput>();

                // Publish response back to Service A
                var response = new BatchProcessResponse
                {
                    CorrelationId = request.CorrelationId,
                    Success = output?.Success ?? false,
                    Message = output?.Message ?? "Unknown error",
                    WorkflowResults = output?.Results ?? new List<WorkflowResult>(),
                    CompletedAt = endTime,
                    TotalDuration = endTime - startTime
                };

                Console.WriteLine($"[{InstanceId}] ========================================");
                Console.WriteLine($"[{InstanceId}] BATCH PROCESSING COMPLETE");
                Console.WriteLine($"[{InstanceId}] CorrelationId: {request.CorrelationId}");
                Console.WriteLine($"[{InstanceId}] Success: {response.Success}");
                Console.WriteLine($"[{InstanceId}] Duration: {response.TotalDuration}");
                Console.WriteLine($"[{InstanceId}] ========================================");

                await daprClient.PublishEventAsync(
                    Constants.PubSubName,
                    Constants.BatchProcessResponseTopic,
                    response);

                logger.LogInformation(
                    "[{Instance}] Published batch response: CorrelationId={CorrelationId}, Success={Success}, Duration={Duration}",
                    InstanceId, response.CorrelationId, response.Success, response.TotalDuration);

                return Results.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{InstanceId}] ERROR: {ex.Message}");
                logger.LogError(ex, "[{Instance}] Error processing batch request {CorrelationId}", InstanceId, request.CorrelationId);

                // Publish failure response
                var response = new BatchProcessResponse
                {
                    CorrelationId = request.CorrelationId,
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    WorkflowResults = new List<WorkflowResult>(),
                    CompletedAt = DateTime.UtcNow,
                    TotalDuration = DateTime.UtcNow - startTime
                };

                await daprClient.PublishEventAsync(
                    Constants.PubSubName,
                    Constants.BatchProcessResponseTopic,
                    response);

                return Results.Ok(); // Return OK to ack the pub/sub message
            }
        })
        .WithName("HandleBatchProcessRequest")
        .WithOpenApi();

        return app;
    }
}
