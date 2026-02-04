namespace ServiceB.Models;

/// <summary>
/// Request from Service A to start batch processing
/// </summary>
public record BatchProcessRequest
{
    public string CorrelationId { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// Response sent back to Service A when all workflows complete
/// </summary>
public record BatchProcessResponse
{
    public string CorrelationId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<WorkflowResult> WorkflowResults { get; init; } = new();
    public DateTime CompletedAt { get; init; }
    public TimeSpan TotalDuration { get; init; }
}

/// <summary>
/// Result from an individual workflow
/// </summary>
public record WorkflowResult
{
    public string WorkflowName { get; init; } = string.Empty;
    public string InstanceId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime CompletedAt { get; init; }
}

/// <summary>
/// Input for the orchestrator workflow that coordinates all parallel workflows
/// </summary>
public record BatchOrchestratorInput
{
    public string CorrelationId { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
}

/// <summary>
/// Output from the orchestrator workflow
/// </summary>
public record BatchOrchestratorOutput
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<WorkflowResult> Results { get; init; } = new();
}

/// <summary>
/// Input for individual child workflows
/// </summary>
public record ChildWorkflowInput
{
    public string ParentCorrelationId { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = string.Empty;
    public int SimulatedDelayMs { get; init; } = 2000;
}

/// <summary>
/// Input for the approval workflow that waits for external event
/// </summary>
public record ApprovalWorkflowInput
{
    public string ParentCorrelationId { get; init; } = string.Empty;
    public string WorkflowInstanceId { get; init; } = string.Empty;
}

/// <summary>
/// External approval event payload
/// </summary>
public record ApprovalEvent
{
    public string ApprovedBy { get; init; } = string.Empty;
    public bool IsApproved { get; init; }
    public string? Comments { get; init; }
    public DateTime ApprovedAt { get; init; }
}

/// <summary>
/// Request to trigger approval for a waiting workflow
/// </summary>
public record TriggerApprovalRequest
{
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public string ApprovedBy { get; init; } = string.Empty;
    public bool IsApproved { get; init; } = true;
    public string? Comments { get; init; }
}
