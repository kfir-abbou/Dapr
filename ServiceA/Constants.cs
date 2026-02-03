namespace ServiceA;

public static class Constants
{
    public const string StateStoreName = "statestore";
    public const string SystemStateKey = "system-state";
    public const string PubSubName = "pubsub";
    public const string TopicName = "system-events";
    public const string ItemRequestTopic = "item-requests";
    public const string ItemResponseTopic = "item-responses";
    public const string WorkflowProgressTopic = "workflow-progress";
    
    public static readonly string[] ValidStates = { "idle", "running", "setup", "procedure", "error" };
    
    public static readonly Dictionary<string, string[]> ValidTransitions = new()
    {
        { "idle", new[] { "setup", "error" } },
        { "setup", new[] { "running", "idle", "error" } },
        { "running", new[] { "procedure", "idle", "error" } },
        { "procedure", new[] { "running", "idle", "error" } },
        { "error", new[] { "idle" } }
    };
}
