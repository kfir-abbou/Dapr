using Dapr.Workflow;
using Dapr.Client;
using ServiceB.Models;

namespace ServiceB.Activities;

public record ServiceCActivityInput
{
    public string CorrelationId { get; init; } = string.Empty;
    public string WorkflowInstanceId { get; init; } = string.Empty;
}

/// <summary>
/// Activity that sends a request to ServiceC and returns immediately (fire-and-forget).
/// The workflow will use WaitForExternalEvent to wait for ServiceC completion.
/// </summary>
public class SendToServiceCActivity : WorkflowActivity<ServiceCActivityInput, WorkflowResult>
{
    private readonly DaprClient _daprClient;

    public SendToServiceCActivity(DaprClient daprClient)
    {
        _daprClient = daprClient;
    }

    public override async Task<WorkflowResult> RunAsync(WorkflowActivityContext context, ServiceCActivityInput input)
    {
        Console.WriteLine($"[ServiceC Activity] ========================================");
        Console.WriteLine($"[ServiceC Activity] Sending request to ServiceC");
        Console.WriteLine($"[ServiceC Activity] Correlation ID: {input.CorrelationId}");
        Console.WriteLine($"[ServiceC Activity] Workflow Instance: {input.WorkflowInstanceId}");
        Console.WriteLine($"[ServiceC Activity] ========================================");

        var request = new ServiceCRequest
        {
            CorrelationId = input.CorrelationId,
            WorkflowInstanceId = input.WorkflowInstanceId,
            RequestedBy = "ServiceB",
            RequestedAt = DateTime.UtcNow
        };

        // Publish request to ServiceC via pub/sub
        await _daprClient.PublishEventAsync(
            Constants.PubSubName,
            Constants.ServiceCRequestTopic,
            request);

        Console.WriteLine($"[ServiceC Activity] Request published to topic '{Constants.ServiceCRequestTopic}'");
        Console.WriteLine($"[ServiceC Activity] Workflow will wait for completion event...");

        return new WorkflowResult
        {
            WorkflowName = "ServiceC-RequestSent",
            InstanceId = input.WorkflowInstanceId,
            Success = true,
            Message = "Request sent to ServiceC, waiting for completion",
            CompletedAt = DateTime.UtcNow
        };
    }
}
