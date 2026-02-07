using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using ServiceA.Models;

namespace ServiceA.Endpoints;

public static class BatchProcessEndpoints
{
    public static WebApplication MapBatchProcessEndpoints(this WebApplication app)
    {
        // Trigger batch processing in Service B via Dapr Pub/Sub
        // This is fully async - ServiceA publishes and ServiceB subscribes
        app.MapPost("/batch/start", async (
            DaprClient daprClient,
            [FromBody] TriggerBatchRequest? request,
            ILogger<Program> logger) =>
        {
            var correlationId = Guid.NewGuid().ToString("N");
            
            var batchRequest = new BatchProcessRequest
            {
                CorrelationId = correlationId,
                RequestedBy = "ServiceA",
                RequestedAt = DateTime.UtcNow,
                Parameters = request?.Parameters
            };

            try
            {
                // Publish to message queue - any ServiceB instance can pick it up
                await daprClient.PublishEventAsync(
                    Constants.PubSubName,
                    Constants.BatchProcessRequestTopic,
                    batchRequest);
                
                logger.LogInformation(
                    "[Batch] Published batch request to topic '{Topic}': CorrelationId={CorrelationId}",
                    Constants.BatchProcessRequestTopic, correlationId);

                return Results.Accepted(value: new
                {
                    Message = "Batch processing request published to message queue",
                    CorrelationId = correlationId,
                    CommunicationMethod = "Pub/Sub (async message queue)",
                    Topic = Constants.BatchProcessRequestTopic,
                    Note = "Any available ServiceB instance will pick up this message. Check /batch/status/{correlationId} for results.",
                    ApprovalInfo = new
                    {
                        WorkflowInstanceId = $"batch-{correlationId}",
                        Endpoint = "POST http://localhost:5002/workflow/approve (or :5012, :5022)",
                        ExampleBody = new
                        {
                            workflowInstanceId = $"batch-{correlationId}",
                            approvedBy = "admin",
                            isApproved = true,
                            comments = "Approved!"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Batch] Error publishing to message queue: {Message}", ex.Message);
                return Results.Problem($"Error publishing request: {ex.Message}");
            }
        })
        .WithName("StartBatchProcess")
        .WithOpenApi();

        // Subscribe to batch processing responses from ServiceB
        app.MapPost("/events/batch-response", 
            [Topic(Constants.PubSubName, Constants.BatchProcessResponseTopic)]
            async (BatchProcessResponse response, DaprClient daprClient, ILogger<Program> logger) =>
        {
            logger.LogInformation(
                "[Batch] Received response from ServiceB: CorrelationId={CorrelationId}, Success={Success}, Duration={Duration}",
                response.CorrelationId, response.Success, response.TotalDuration);

            Console.WriteLine($"[Batch Response] ========================================");
            Console.WriteLine($"[Batch Response] CorrelationId: {response.CorrelationId}");
            Console.WriteLine($"[Batch Response] Success: {response.Success}");
            Console.WriteLine($"[Batch Response] Message: {response.Message}");
            Console.WriteLine($"[Batch Response] Duration: {response.TotalDuration}");
            Console.WriteLine($"[Batch Response] Results:");
            foreach (var result in response.WorkflowResults)
            {
                var status = result.Success ? "✓" : "✗";
                Console.WriteLine($"[Batch Response]   {status} {result.WorkflowName}: {result.Message}");
            }
            Console.WriteLine($"[Batch Response] ========================================");

            // Store the response in state store for later retrieval
            var stateKey = $"batch-response-{response.CorrelationId}";
            await daprClient.SaveStateAsync(Constants.StateStoreName, stateKey, response);
            
            logger.LogInformation("[Batch] Response saved to state store: {StateKey}", stateKey);

            return Results.Ok(new { received = true, correlationId = response.CorrelationId });
        })
        .WithName("ReceiveBatchResponse")
        .ExcludeFromDescription(); // Hide from Swagger since it's for pub/sub

        // Get batch processing status/result
        app.MapGet("/batch/status/{correlationId}", async (
            string correlationId,
            DaprClient daprClient,
            ILogger<Program> logger) =>
        {
            var stateKey = $"batch-response-{correlationId}";
            var response = await daprClient.GetStateAsync<BatchProcessResponse>(Constants.StateStoreName, stateKey);

            if (response == null)
            {
                return Results.Ok(new
                {
                    CorrelationId = correlationId,
                    Status = "pending",
                    Message = "Batch processing is still in progress or not found"
                });
            }

            return Results.Ok(new
            {
                CorrelationId = correlationId,
                Status = response.Success ? "completed" : "failed",
                response.Success,
                response.Message,
                response.TotalDuration,
                response.CompletedAt,
                Results = response.WorkflowResults
            });
        })
        .WithName("GetBatchStatus")
        .WithOpenApi();

        return app;
    }
}
