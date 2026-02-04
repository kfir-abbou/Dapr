using Dapr.Workflow;
using ServiceB.Models;

namespace ServiceB.Activities;

public record DelayActivityInput
{
    public string WorkflowName { get; init; } = string.Empty;
    public int DelayMs { get; init; }
    public string InstanceId { get; init; } = string.Empty;
}

/// <summary>
/// Simple activity that simulates work with a configurable delay
/// </summary>
public class DelayActivity : WorkflowActivity<DelayActivityInput, WorkflowResult>
{
    public override async Task<WorkflowResult> RunAsync(WorkflowActivityContext context, DelayActivityInput input)
    {
        var startTime = DateTime.UtcNow;
        Console.WriteLine($"[Activity] [{input.WorkflowName}] Started at {startTime:HH:mm:ss.fff} (duration: {input.DelayMs}ms)");
        
        // Simulate work with progress updates
        var delayMs = input.DelayMs;
        var elapsed = 0;
        var progressInterval = Math.Max(500, delayMs / 4); // Report progress 4 times or every 500ms
        
        while (elapsed < delayMs)
        {
            var remaining = delayMs - elapsed;
            var sleepTime = Math.Min(progressInterval, remaining);
            await Task.Delay(sleepTime);
            elapsed += sleepTime;
            
            var percentComplete = (int)((double)elapsed / delayMs * 100);
            Console.WriteLine($"[Activity] [{input.WorkflowName}] Progress: {percentComplete}% ({elapsed}ms / {delayMs}ms)");
        }
        
        var endTime = DateTime.UtcNow;
        var actualDuration = endTime - startTime;
        Console.WriteLine($"[Activity] [{input.WorkflowName}] ✓ Completed at {endTime:HH:mm:ss.fff} (actual: {actualDuration.TotalMilliseconds:F0}ms)");
        
        return new WorkflowResult
        {
            WorkflowName = input.WorkflowName,
            InstanceId = input.InstanceId,
            Success = true,
            Message = $"{input.WorkflowName} completed after {actualDuration.TotalMilliseconds:F0}ms",
            CompletedAt = endTime
        };
    }
}
