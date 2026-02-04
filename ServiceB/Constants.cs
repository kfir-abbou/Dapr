namespace ServiceB;

public static class Constants
{
    public const string PubSubName = "pubsub";
    public const string SystemEventsTopic = "system-events";
    public const string ItemRequestTopic = "item-requests";
    public const string ItemResponseTopic = "item-responses";
    
    // Batch processing topics
    public const string BatchProcessRequestTopic = "batch-process-request";
    public const string BatchProcessResponseTopic = "batch-process-response";
    
    // Workflow external events
    public const string ApprovalEventName = "ApprovalReceived";
    
    // Timeouts
    public static readonly TimeSpan ApprovalTimeout = TimeSpan.FromMinutes(3);
}
