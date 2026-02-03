using Dapr.Client;
using Dapr.Workflow;
using ServiceA.Models;
using ServiceA.Workflows;

namespace ServiceA.Activities;

public abstract class BaseActivity : WorkflowActivity<ActivityInput, ActivityResult>
{
    protected readonly DaprClient DaprClient;

    protected BaseActivity(DaprClient daprClient)
    {
        DaprClient = daprClient;
    }

    protected async Task PublishProgressAsync(ActivityInput input, int percentComplete, string message)
    {
        await DaprClient.PublishEventAsync(Constants.PubSubName, Constants.WorkflowProgressTopic, new WorkflowProgressEvent
        {
            WorkflowInstanceId = input.WorkflowInstanceId,
            WorkflowName = nameof(SetupWorkflow),
            ActivityName = GetType().Name,
            PercentComplete = percentComplete,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    protected int CalculateStartPercent(ActivityInput input) =>
        (int)((double)input.StepNumber / input.TotalSteps * 100) - 25 + 5;

    protected int CalculateCompletePercent(ActivityInput input) =>
        (int)((double)input.StepNumber / input.TotalSteps * 100);
}
