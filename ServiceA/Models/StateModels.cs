namespace ServiceA.Models;

public record SystemState
{
    public string Status { get; init; } = "idle";
    public DateTime LastUpdated { get; init; }
    public string? PreviousStatus { get; init; }
}

public record SetStateRequest
{
    public string Status { get; init; } = string.Empty;
}

public record StateChangeResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? CurrentState { get; init; }
    public string? RequestedState { get; init; }
    public string? PreviousState { get; init; }
}

public record SystemEvent
{
    public string EventType { get; init; } = string.Empty;
    public string? PreviousState { get; init; }
    public string NewState { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
