using Dapr;
using Dapr.Client;
using ServiceB.Models;
using ServiceB.Stores;

namespace ServiceB.Endpoints;

public static class EventSubscribers
{
    public static WebApplication MapEventSubscribers(this WebApplication app)
    {
        // Subscribe to system events from ServiceA
        app.MapPost("/events/system", [Topic(Constants.PubSubName, Constants.SystemEventsTopic)] 
            (SystemEvent @event, ILogger<Program> logger) =>
        {
            logger.LogInformation("Received system event: {EventType} - New State: {NewState} at {Timestamp}",
                @event.EventType, @event.NewState, @event.Timestamp);
            
            // Handle the event (e.g., adjust behavior based on system state)
            return Results.Ok();
        })
        .WithName("HandleSystemEvent")
        .WithOpenApi();

        // Subscribe to item processing requests from ServiceA
        app.MapPost("/events/item-request", [Topic(Constants.PubSubName, Constants.ItemRequestTopic)] 
            async (ItemProcessMessage request, ItemStore store, DaprClient daprClient, ILogger<Program> logger) =>
        {
            logger.LogInformation(
                "[PubSub] Received item process request: CorrelationId={CorrelationId}, Operation={Operation}, ItemId={ItemId}",
                request.CorrelationId, request.Operation, request.ItemId);
            
            // Simulate processing time
            await Task.Delay(2000);
            
            // Process based on operation
            var item = store.GetById(request.ItemId);
            var (success, message) = await ProcessItemAsync(item, request.Operation);
            
            // Publish response back to ServiceA
            var response = new ItemProcessResponse
            {
                CorrelationId = request.CorrelationId,
                ItemId = request.ItemId,
                Operation = request.Operation,
                Success = success,
                Message = message,
                ProcessedAt = DateTime.UtcNow
            };
            
            await daprClient.PublishEventAsync(Constants.PubSubName, Constants.ItemResponseTopic, response);
            
            logger.LogInformation(
                "[PubSub] Published item response: CorrelationId={CorrelationId}, Success={Success}",
                response.CorrelationId, response.Success);
            
            return Results.Ok();
        })
        .WithName("HandleItemRequest")
        .WithOpenApi();

        return app;
    }

    private static async Task<(bool Success, string Message)> ProcessItemAsync(Item? item, string operation)
    {
        if (item == null)
        {
            return (false, "Item not found");
        }

        return operation.ToLower() switch
        {
            "validate" => await ValidateItemAsync(item),
            "enrich" => await EnrichItemAsync(item),
            "archive" => await ArchiveItemAsync(item),
            _ => (false, $"Unknown operation: {operation}")
        };
    }

    private static async Task<(bool Success, string Message)> ValidateItemAsync(Item item)
    {
        await Task.Delay(1000); // Simulate validation
        return (true, $"Item '{item.Name}' validated successfully. Price: {item.Price:C}");
    }

    private static async Task<(bool Success, string Message)> EnrichItemAsync(Item item)
    {
        await Task.Delay(1500); // Simulate enrichment
        return (true, $"Item '{item.Name}' enriched with additional metadata");
    }

    private static async Task<(bool Success, string Message)> ArchiveItemAsync(Item item)
    {
        await Task.Delay(500); // Simulate archiving
        return (true, $"Item '{item.Name}' archived successfully");
    }
}
