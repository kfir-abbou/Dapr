using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add Dapr client with configuration from environment variables
builder.Services.AddDaprClient(daprClientBuilder =>
{
    // Dapr sidecar sets DAPR_HTTP_PORT and DAPR_GRPC_PORT environment variables
    var daprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500";
    var daprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "50002";
    
    daprClientBuilder.UseHttpEndpoint($"http://localhost:{daprHttpPort}");
    daprClientBuilder.UseGrpcEndpoint($"http://localhost:{daprGrpcPort}");
});

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add in-memory data store
builder.Services.AddSingleton<ItemStore>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable Dapr pub/sub
app.UseCloudEvents();
app.MapSubscribeHandler();

const string PubSubName = "pubsub";
const string TopicName = "system-events";

// ==================== Item Endpoints ====================

// Get all items
app.MapGet("/items", (ItemStore store) =>
{
    return Results.Ok(store.GetAll());
})
.WithName("GetAllItems")
.WithOpenApi();

// Get item by id
app.MapGet("/items/{id:int}", (ItemStore store, int id) =>
{
    var item = store.GetById(id);
    return item is not null ? Results.Ok(item) : Results.NotFound();
})
.WithName("GetItemById")
.WithOpenApi();

// Create new item
app.MapPost("/items", (ItemStore store, [FromBody] CreateItemRequest request) =>
{
    var item = store.Add(request);
    return Results.Created($"/items/{item.Id}", item);
})
.WithName("CreateItem")
.WithOpenApi();

// Update item
app.MapPut("/items/{id:int}", (ItemStore store, int id, [FromBody] UpdateItemRequest request) =>
{
    var item = store.Update(id, request);
    return item is not null ? Results.Ok(item) : Results.NotFound();
})
.WithName("UpdateItem")
.WithOpenApi();

// Delete item
app.MapDelete("/items/{id:int}", (ItemStore store, int id) =>
{
    var deleted = store.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteItem")
.WithOpenApi();

// ==================== Pub/Sub Subscription ====================

// Subscribe to system events from ServiceA
app.MapPost("/events/system", [Topic(PubSubName, TopicName)] (SystemEvent @event, ILogger<Program> logger) =>
{
    logger.LogInformation("Received system event: {EventType} - New State: {NewState} at {Timestamp}",
        @event.EventType, @event.NewState, @event.Timestamp);
    
    // Handle the event (e.g., adjust behavior based on system state)
    return Results.Ok();
})
.WithName("HandleSystemEvent")
.WithOpenApi();

// ==================== Health Endpoint ====================

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ServiceB", Timestamp = DateTime.UtcNow }))
.WithName("Health")
.WithOpenApi();

app.Run();

// ==================== Models ====================

public record Item
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public record CreateItemRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public record UpdateItemRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public record SystemEvent
{
    public string EventType { get; init; } = string.Empty;
    public string NewState { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

// ==================== In-Memory Store ====================

public class ItemStore
{
    private readonly List<Item> _items;
    private int _nextId;
    private readonly object _lock = new();

    public ItemStore()
    {
        // Seed with sample data
        _items = new List<Item>
        {
            new Item { Id = 1, Name = "Widget A", Description = "A standard widget", Price = 9.99m },
            new Item { Id = 2, Name = "Widget B", Description = "A premium widget", Price = 19.99m },
            new Item { Id = 3, Name = "Gadget X", Description = "An advanced gadget", Price = 49.99m }
        };
        _nextId = 4;
    }

    public List<Item> GetAll()
    {
        lock (_lock)
        {
            return _items.ToList();
        }
    }

    public Item? GetById(int id)
    {
        lock (_lock)
        {
            return _items.FirstOrDefault(i => i.Id == id);
        }
    }

    public Item Add(CreateItemRequest request)
    {
        lock (_lock)
        {
            var item = new Item
            {
                Id = _nextId++,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price
            };
            _items.Add(item);
            return item;
        }
    }

    public Item? Update(int id, UpdateItemRequest request)
    {
        lock (_lock)
        {
            var index = _items.FindIndex(i => i.Id == id);
            if (index == -1) return null;

            var updated = new Item
            {
                Id = id,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price
            };
            _items[index] = updated;
            return updated;
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item is null) return false;
            _items.Remove(item);
            return true;
        }
    }
}
