using Dapr.Client;
using ServiceA.Models;
using System.Text.Json;

namespace ServiceA.Services;

public class StateBackupService
{
    private readonly DaprClient _daprClient;
    private readonly string _bindingName;
    private readonly string _fileName;

    public StateBackupService(DaprClient daprClient, IConfiguration configuration)
    {
        _daprClient = daprClient;
        _bindingName = configuration["StateBackup:BindingName"] ?? "statebackup";
        _fileName = configuration["StateBackup:FileName"] ?? "system-state.json";
    }

    public async Task SaveStateToFileAsync(SystemState state)
    {
        var stateJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        var metadata = new Dictionary<string, string>
        {
            { "fileName", _fileName }
        };

        await _daprClient.InvokeBindingAsync(
            _bindingName,
            "create",
            stateJson,
            metadata);
    }

    public async Task InitializeStateAsync()
    {
        try
        {
            var state = await _daprClient.GetStateAsync<SystemState>(Constants.StateStoreName, Constants.SystemStateKey);
            var currentState = state ?? new SystemState { Status = "idle", LastUpdated = DateTime.UtcNow };

            if (state == null)
            {
                await _daprClient.SaveStateAsync(Constants.StateStoreName, Constants.SystemStateKey, currentState);
            }

            await SaveStateToFileAsync(currentState);
            Console.WriteLine($"[StateBackup] Initial state saved to file: {_fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StateBackup] Error saving initial state: {ex.Message}");
        }
    }
}
