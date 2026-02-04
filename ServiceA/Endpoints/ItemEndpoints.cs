using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using ServiceA.Models;

namespace ServiceA.Endpoints;

public static class ItemEndpoints
{
    public static void MapItemEndpoints(this WebApplication app)
    {
        app.MapGet("/items", GetAllItems)
            .WithName("GetItems")
            .WithOpenApi();

        app.MapGet("/items/{id:int}", GetItemById)
            .WithName("GetItemById")
            .WithOpenApi();

        app.MapPost("/items/process", ProcessItem)
            .WithName("RequestItemProcessing")
            .WithOpenApi();
    }

    private static async Task<IResult> GetAllItems(IHttpClientFactory httpClientFactory)
    {
        try
        {
            var client = httpClientFactory.CreateClient("ServiceB");
            var items = await client.GetFromJsonAsync<List<Item>>("items");
            return Results.Ok(items);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error calling ServiceB: {ex.Message}");
        }
    }

    private static async Task<IResult> GetItemById(IHttpClientFactory httpClientFactory, int id)
    {
        try
        {
            var client = httpClientFactory.CreateClient("ServiceB");
            var response = await client.GetAsync($"items/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Results.NotFound($"Item {id} not found");
            }
            response.EnsureSuccessStatusCode();
            var item = await response.Content.ReadFromJsonAsync<Item>();
            return Results.Ok(item);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error calling ServiceB: {ex.Message}");
        }
    }

    private static async Task<IResult> ProcessItem(DaprClient daprClient, [FromBody] ItemProcessRequest request)
    {
        var correlationId =  Guid.NewGuid().ToString();

        var message = new ItemProcessMessage
        {
            CorrelationId = correlationId,
            ItemId = request.ItemId,
            Operation = request.Operation,
            RequestedBy = "ServiceA",
            RequestedAt = DateTime.UtcNow
        };

        await daprClient.PublishEventAsync(Constants.PubSubName, Constants.ItemRequestTopic, message);

        Console.WriteLine($"[PubSub] Published item process request: {correlationId} - Operation: {request.Operation} on Item {request.ItemId}");

        return Results.Accepted(value: new
        {
            Message = "Item processing request submitted",
            CorrelationId = correlationId,
            Operation = request.Operation,
            ItemId = request.ItemId
        });
    }
}
