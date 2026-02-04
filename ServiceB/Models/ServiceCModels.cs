namespace ServiceB.Models;

/// <summary>
/// Request sent to ServiceC to start data processing
/// </summary>
public record ServiceCRequest
{
    public string CorrelationId { get; init; } = string.Empty;
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}

/// <summary>
/// Progress update received from ServiceC
/// </summary>
public record ServiceCProgress
{
    public string CorrelationId { get; init; } = string.Empty;
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public string StepName { get; init; } = string.Empty;
    public int StepNumber { get; init; }
    public int TotalSteps { get; init; }
    public int PercentComplete { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Completion event received from ServiceC
/// </summary>
public record ServiceCComplete
{
    public string CorrelationId { get; init; } = string.Empty;
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public double TotalDurationSeconds { get; init; }
    public DateTime CompletedAt { get; init; }
}
