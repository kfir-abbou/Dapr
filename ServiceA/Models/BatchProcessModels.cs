namespace ServiceA.Models;

/// <summary>
/// Request to start batch processing in Service B
/// </summary>
public record BatchProcessRequest
{
    public string CorrelationId { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// Response from Service B when all workflows complete
/// </summary>
public record BatchProcessResponse
{
    public string CorrelationId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<WorkflowResultInfo> WorkflowResults { get; init; } = new();
    public DateTime CompletedAt { get; init; }
    public TimeSpan TotalDuration { get; init; }
}

/// <summary>
/// Result from an individual workflow
/// </summary>
public record WorkflowResultInfo
{
    public string WorkflowName { get; init; } = string.Empty;
    public string InstanceId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime CompletedAt { get; init; }
}

/// <summary>
/// Request to trigger batch processing
/// </summary>
public record TriggerBatchRequest
{
    public Dictionary<string, object>? Parameters { get; init; }
}
