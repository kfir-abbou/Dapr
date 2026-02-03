using Dapr.Client;
using Dapr.Workflow;
using ServiceA.Models;

namespace ServiceA.Activities;

public class InitializeSystemActivity : BaseActivity
{
    public InitializeSystemActivity(DaprClient daprClient) : base(daprClient)
    {
    }

    public override async Task<ActivityResult> RunAsync(WorkflowActivityContext context, ActivityInput input)
    {
        await PublishProgressAsync(input, CalculateStartPercent(input), $"Starting: {input.Description}");

        Console.WriteLine($"[Activity] InitializeSystemActivity: {input.Description}");

        // Simulate work
        await Task.Delay(3000);

        await PublishProgressAsync(input, CalculateCompletePercent(input), "System initialized successfully");

        return new ActivityResult
        {
            ActivityName = nameof(InitializeSystemActivity),
            Success = true,
            Message = "System initialized successfully",
            CompletedAt = DateTime.UtcNow
        };
    }
}
