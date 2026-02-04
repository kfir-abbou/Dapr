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
    
    // ServiceC topics
    public const string ServiceCRequestTopic = "servicec-request";
    public const string ServiceCProgressTopic = "servicec-progress";
    public const string ServiceCCompleteTopic = "servicec-complete";
    
    // Workflow external events
    public const string ApprovalEventName = "ApprovalReceived";
    public const string ServiceCCompleteEventName = "ServiceCComplete";
    
    // Timeouts
    public static readonly TimeSpan ApprovalTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan ServiceCTimeout = TimeSpan.FromMinutes(5); // ServiceC can take up to 5 min
}
