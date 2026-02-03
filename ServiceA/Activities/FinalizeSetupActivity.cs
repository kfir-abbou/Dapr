using Dapr.Client;
using Dapr.Workflow;
using ServiceA.Models;

namespace ServiceA.Activities;

public class FinalizeSetupActivity : BaseActivity
{
    public FinalizeSetupActivity(DaprClient daprClient) : base(daprClient)
    {
    }

    public override async Task<ActivityResult> RunAsync(WorkflowActivityContext context, ActivityInput input)
    {
        await PublishProgressAsync(input, CalculateStartPercent(input), $"Starting: {input.Description}");

        Console.WriteLine($"[Activity] FinalizeSetupActivity: {input.Description}");

        // Simulate work
        await Task.Delay(1500);

        await PublishProgressAsync(input, CalculateCompletePercent(input), "Setup finalized successfully - Workflow complete!");

        return new ActivityResult
        {
            ActivityName = nameof(FinalizeSetupActivity),
            Success = true,
            Message = "Setup finalized successfully",
            CompletedAt = DateTime.UtcNow
        };
    }
}
