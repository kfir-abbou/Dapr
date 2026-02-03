using Dapr.Workflow;
using ServiceA.Activities;
using ServiceA.Models;

namespace ServiceA.Workflows;

public class SetupWorkflow : Workflow<SetupWorkflowInput, string>
{
    private const int TotalSteps = 4;

    public override async Task<string> RunAsync(WorkflowContext context, SetupWorkflowInput input)
    {
        var results = new List<ActivityResult>();

        // Step 1: Initialize System (25%)
        var initResult = await context.CallActivityAsync<ActivityResult>(
            nameof(InitializeSystemActivity),
            new ActivityInput
            {
                WorkflowInstanceId = input.WorkflowInstanceId,
                Description = "Starting system initialization",
                StepNumber = 1,
                TotalSteps = TotalSteps
            });
        results.Add(initResult);

        // Step 2: Configure Parameters (50%)
        var configResult = await context.CallActivityAsync<ActivityResult>(
            nameof(ConfigureParametersActivity),
            new ActivityInput
            {
                WorkflowInstanceId = input.WorkflowInstanceId,
                Description = "Configuring system parameters",
                StepNumber = 2,
                TotalSteps = TotalSteps
            });
        results.Add(configResult);

        // Step 3: Validate Configuration (75%)
        var validateResult = await context.CallActivityAsync<ActivityResult>(
            nameof(ValidateConfigurationActivity),
            new ActivityInput
            {
                WorkflowInstanceId = input.WorkflowInstanceId,
                Description = "Validating configuration",
                StepNumber = 3,
                TotalSteps = TotalSteps
            });
        results.Add(validateResult);

        // Step 4: Finalize Setup (100%)
        var finalizeResult = await context.CallActivityAsync<ActivityResult>(
            nameof(FinalizeSetupActivity),
            new ActivityInput
            {
                WorkflowInstanceId = input.WorkflowInstanceId,
                Description = "Finalizing setup",
                StepNumber = 4,
                TotalSteps = TotalSteps
            });
        results.Add(finalizeResult);

        return $"Setup completed successfully. {results.Count} activities executed.";
    }
}
