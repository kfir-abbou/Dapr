using Dapr.Client;
using Dapr.Workflow;
using ServiceA.Models;

namespace ServiceA.Activities;

public class ConfigureParametersActivity : BaseActivity
{
    public ConfigureParametersActivity(DaprClient daprClient) : base(daprClient)
    {
    }

    public override async Task<ActivityResult> RunAsync(WorkflowActivityContext context, ActivityInput input)
    {
        await PublishProgressAsync(input, CalculateStartPercent(input), $"Starting: {input.Description}");

        Console.WriteLine($"[Activity] ConfigureParametersActivity: {input.Description}");

        // Simulate work
        await Task.Delay(2000);

        await PublishProgressAsync(input, CalculateCompletePercent(input), "Parameters configured successfully");

        return new ActivityResult
        {
            ActivityName = nameof(ConfigureParametersActivity),
            Success = true,
            Message = "Parameters configured successfully",
            CompletedAt = DateTime.UtcNow
        };
    }
}
