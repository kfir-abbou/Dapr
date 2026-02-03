using Dapr.Client;
using Dapr.Workflow;
using ServiceA.Models;

namespace ServiceA.Activities;

public class ValidateConfigurationActivity : BaseActivity
{
    public ValidateConfigurationActivity(DaprClient daprClient) : base(daprClient)
    {
    }

    public override async Task<ActivityResult> RunAsync(WorkflowActivityContext context, ActivityInput input)
    {
        await PublishProgressAsync(input, CalculateStartPercent(input), $"Starting: {input.Description}");

        Console.WriteLine($"[Activity] ValidateConfigurationActivity: {input.Description}");

        // Simulate work
        await Task.Delay(2500);

        await PublishProgressAsync(input, CalculateCompletePercent(input), "Configuration validated successfully");

        return new ActivityResult
        {
            ActivityName = nameof(ValidateConfigurationActivity),
            Success = true,
            Message = "Configuration validated successfully",
            CompletedAt = DateTime.UtcNow
        };
    }
}
