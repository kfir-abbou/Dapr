namespace ServiceA.Models;

public record Item
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public record ItemProcessRequest
{
    public int ItemId { get; init; }
    public string Operation { get; init; } = string.Empty; // "validate", "enrich", "archive"
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
