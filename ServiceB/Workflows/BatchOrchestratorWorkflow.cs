using Dapr.Workflow;
using ServiceB.Models;
using ServiceB.Activities;

namespace ServiceB.Workflows;

/// <summary>
/// Main orchestrator workflow that runs multiple activities/workflows in parallel
/// and waits for all of them to complete using Task.WhenAll
/// </summary>
public class BatchOrchestratorWorkflow : Workflow<BatchOrchestratorInput, BatchOrchestratorOutput>
{
    // Get instance ID from environment variable (set in dapr.yaml)
    private static readonly string ServiceInstanceId = Environment.GetEnvironmentVariable("INSTANCE_ID") ?? $"ServiceB-{Environment.ProcessId}";
    
    public override async Task<BatchOrchestratorOutput> RunAsync(WorkflowContext context, BatchOrchestratorInput input)
    {
        var results = new List<WorkflowResult>();
        
        // The approval workflow instance ID - needed for external event
        var approvalId = $"approval-{context.InstanceId}";

        Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] ========================================");
        Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] Starting BatchOrchestratorWorkflow");
        Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] Instance ID: {context.InstanceId}");
        Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] Correlation ID: {input.CorrelationId}");
        Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] Requested by: {input.RequestedBy}");
        Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] ========================================");
        Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] Starting 5 parallel tasks...");

        try
        {
            // Start multiple activities in parallel
            // Task 1: ServiceC - Fire and forget + wait for completion event
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] [1/5] Starting ServiceC DataProcessing (fire-and-forget + wait for completion)...");
            var serviceCTask = CallServiceCAsync(context, input.CorrelationId);

            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] [2/5] Starting Validation activity (2s delay)...");
            var validationTask = context.CallActivityAsync<WorkflowResult>(
                nameof(DelayActivity),
                new DelayActivityInput 
                { 
                    WorkflowName = "Validation",
                    DelayMs = 2000,
                    InstanceId = $"validation-{context.InstanceId}"
                });

            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] [3/5] Starting Enrichment activity (4s delay)...");
            var enrichmentTask = context.CallActivityAsync<WorkflowResult>(
                nameof(DelayActivity),
                new DelayActivityInput 
                { 
                    WorkflowName = "Enrichment",
                    DelayMs = 4000,
                    InstanceId = $"enrichment-{context.InstanceId}"
                });

            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] [4/5] Starting Notification activity (1.5s delay)...");
            var notificationTask = context.CallActivityAsync<WorkflowResult>(
                nameof(DelayActivity),
                new DelayActivityInput 
                { 
                    WorkflowName = "Notification",
                    DelayMs = 1500,
                    InstanceId = $"notification-{context.InstanceId}"
                });

            // Wait for the external approval event (with timeout)
            // This runs in parallel with the other activities
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] [5/5] Starting Approval task (waiting for external event, timeout: {Constants.ApprovalTimeout.TotalMinutes} min)...");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] ----------------------------------------");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] >>> To approve, POST to: http://localhost:5002/workflow/approve");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] >>> With body: {{ \"workflowInstanceId\": \"{context.InstanceId}\", \"approvedBy\": \"admin\", \"isApproved\": true }}");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] ----------------------------------------");
            var approvalTask = WaitForApprovalAsync(context, approvalId);

            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] All 5 tasks started. Waiting for completion (Task.WhenAll)...");

            // Wait for ALL to complete
            await Task.WhenAll(
                serviceCTask,
                validationTask,
                enrichmentTask,
                notificationTask,
                approvalTask);

            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] All tasks completed! Collecting results...");

            // Collect results
            results.Add(await serviceCTask);
            results.Add(await validationTask);
            results.Add(await enrichmentTask);
            results.Add(await notificationTask);
            results.Add(await approvalTask);

            var allSucceeded = results.All(r => r.Success);
            var successCount = results.Count(r => r.Success);
            var failedCount = results.Count(r => !r.Success);

            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] ========================================");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] WORKFLOW COMPLETE");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] Success: {allSucceeded}");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] Tasks succeeded: {successCount}/{results.Count}");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] Tasks failed: {failedCount}/{results.Count}");
            foreach (var result in results)
            {
                var status = result.Success ? "✓" : "✗";
                Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator]   {status} {result.WorkflowName}: {result.Message}");
            }
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] ========================================");

            return new BatchOrchestratorOutput
            {
                Success = allSucceeded,
                Message = allSucceeded 
                    ? "All workflows completed successfully" 
                    : $"Some workflows failed ({failedCount}/{results.Count})",
                Results = results
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] ========================================");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] WORKFLOW FAILED");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] Error: {ex.Message}");
            Console.WriteLine($"[{ServiceInstanceId}] [Orchestrator] ========================================");

            return new BatchOrchestratorOutput
            {
                Success = false,
                Message = $"Orchestrator failed: {ex.Message}",
                Results = results
            };
        }
    }

    private async Task<WorkflowResult> WaitForApprovalAsync(WorkflowContext context, string approvalId)
    {
        Console.WriteLine($"[{ServiceInstanceId}] [ApprovalWorkflow] Waiting for external approval. Instance: {approvalId}");
        
        try
        {
            // Wait for the external approval event with a 3-minute timeout
            var approval = await context.WaitForExternalEventAsync<ApprovalEvent>(
                Constants.ApprovalEventName,
                Constants.ApprovalTimeout);

            if (approval.IsApproved)
            {
                Console.WriteLine($"[{ServiceInstanceId}] [ApprovalWorkflow] Approved by {approval.ApprovedBy}");
                
                return new WorkflowResult
                {
                    WorkflowName = "Approval",
                    InstanceId = approvalId,
                    Success = true,
                    Message = $"Approved by {approval.ApprovedBy}" + 
                              (string.IsNullOrEmpty(approval.Comments) ? "" : $": {approval.Comments}"),
                    CompletedAt = DateTime.UtcNow
                };
            }
            else
            {
                Console.WriteLine($"[{ServiceInstanceId}] [ApprovalWorkflow] Rejected by {approval.ApprovedBy}");
                
                return new WorkflowResult
                {
                    WorkflowName = "Approval",
                    InstanceId = approvalId,
                    Success = false,
                    Message = $"Rejected by {approval.ApprovedBy}" + 
                              (string.IsNullOrEmpty(approval.Comments) ? "" : $": {approval.Comments}"),
                    CompletedAt = DateTime.UtcNow
                };
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"[{ServiceInstanceId}] [ApprovalWorkflow] Timeout - no approval received within {Constants.ApprovalTimeout.TotalMinutes} minutes");
            
            return new WorkflowResult
            {
                WorkflowName = "Approval",
                InstanceId = approvalId,
                Success = false,
                Message = $"Approval timeout - no response within {Constants.ApprovalTimeout.TotalMinutes} minutes",
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Fire-and-forget call to ServiceC, then wait for completion event
    /// </summary>
    private async Task<WorkflowResult> CallServiceCAsync(WorkflowContext context, string correlationId)
    {
        var instanceId = $"servicec-{context.InstanceId}";
        Console.WriteLine($"[{ServiceInstanceId}] [ServiceCWorkflow] Starting ServiceC call. Instance: {instanceId}");
        
        try
        {
            // Fire-and-forget: Send request to ServiceC via pub/sub
            await context.CallActivityAsync(
                nameof(SendToServiceCActivity),
                new ServiceCActivityInput
                {
                    CorrelationId = correlationId,
                    WorkflowInstanceId = context.InstanceId
                });

            Console.WriteLine($"[{ServiceInstanceId}] [ServiceCWorkflow] Request sent to ServiceC. Waiting for completion event (timeout: {Constants.ServiceCTimeout.TotalMinutes} min)...");
            Console.WriteLine($"[{ServiceInstanceId}] [ServiceCWorkflow] >>> ServiceC will publish progress events to servicec-progress topic");
            Console.WriteLine($"[{ServiceInstanceId}] [ServiceCWorkflow] >>> ServiceC will publish completion event to servicec-complete topic");

            // Wait for the completion event from ServiceC
            var completion = await context.WaitForExternalEventAsync<ServiceCComplete>(
                Constants.ServiceCCompleteEventName,
                Constants.ServiceCTimeout);

            if (completion.Success)
            {
                Console.WriteLine($"[{ServiceInstanceId}] [ServiceCWorkflow] ServiceC completed successfully: {completion.Message}");
                
                return new WorkflowResult
                {
                    WorkflowName = "ServiceC-DataProcessing",
                    InstanceId = instanceId,
                    Success = true,
                    Message = completion.Message,
                    CompletedAt = DateTime.UtcNow
                };
            }
            else
            {
                Console.WriteLine($"[{ServiceInstanceId}] [ServiceCWorkflow] ServiceC completed with failure: {completion.Message}");
                
                return new WorkflowResult
                {
                    WorkflowName = "ServiceC-DataProcessing",
                    InstanceId = instanceId,
                    Success = false,
                    Message = completion.Message,
                    CompletedAt = DateTime.UtcNow
                };
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"[{ServiceInstanceId}] [ServiceCWorkflow] Timeout - no completion from ServiceC within {Constants.ServiceCTimeout.TotalMinutes} minutes");
            
            return new WorkflowResult
            {
                WorkflowName = "ServiceC-DataProcessing",
                InstanceId = instanceId,
                Success = false,
                Message = $"ServiceC timeout - no completion within {Constants.ServiceCTimeout.TotalMinutes} minutes",
                CompletedAt = DateTime.UtcNow
            };
        }
    }
}
