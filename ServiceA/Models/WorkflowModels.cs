namespace ServiceA.Models;

public record SetupWorkflowInput
{
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
}

public record WorkflowProgressEvent
{
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = string.Empty;
    public string ActivityName { get; init; } = string.Empty;
    public int PercentComplete { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public record ActivityInput
{
    public string WorkflowInstanceId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int StepNumber { get; init; }
    public int TotalSteps { get; init; }
}

public record ActivityResult
{
    public string ActivityName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime CompletedAt { get; init; }
}
