using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using ServiceA.Models;

namespace ServiceA.Endpoints;

public static class BatchProcessEndpoints
{
    public static WebApplication MapBatchProcessEndpoints(this WebApplication app)
    {
        // Trigger batch processing in Service B via direct HTTP call
        app.MapPost("/batch/start", async (
            IHttpClientFactory httpClientFactory,
            [FromBody] TriggerBatchRequest? request,
            ILogger<Program> logger) =>
        {
            // TODO: Remove fixed ID after testing - use Guid.NewGuid().ToString("N") in production
            var correlationId = "test123"; // Fixed ID for Postman testing
            
            var batchRequest = new BatchProcessRequest
            {
                CorrelationId = correlationId,
                RequestedBy = "ServiceA",
                RequestedAt = DateTime.UtcNow,
                Parameters = request?.Parameters
            };

            try
            {
                // Send direct HTTP request to Service B
                var client = httpClientFactory.CreateClient("ServiceB");
                var response = await client.PostAsJsonAsync("events/batch-process", batchRequest);
                
                logger.LogInformation(
                    "[Batch] Started batch processing request: CorrelationId={CorrelationId}, StatusCode={StatusCode}",
                    correlationId, response.StatusCode);

                return Results.Accepted(value: new
                {
                    Message = "Batch processing request submitted",
                    CorrelationId = correlationId,
                    Note = "Service B will start 5 parallel tasks. One requires manual approval.",
                    ApprovalInfo = new
                    {
                        WorkflowInstanceId = $"batch-{correlationId}",
                        Endpoint = "POST http://localhost:5002/workflow/approve",
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
                logger.LogError(ex, "[Batch] Error sending request to ServiceB: {Message}", ex.Message);
                return Results.Problem($"Error sending request to ServiceB: {ex.Message}");
            }
        })
        .WithName("StartBatchProcess")
        .WithOpenApi();

        return app;
    }
}
