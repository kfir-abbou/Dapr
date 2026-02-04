namespace ServiceB.Models;

public record SystemEvent
{
    public string EventType { get; init; } = string.Empty;
    public string NewState { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public record ItemProcessMessage
{
    public string CorrelationId { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string Operation { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}

public record ItemProcessResponse
{
    public string CorrelationId { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string Operation { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime ProcessedAt { get; init; }
}
