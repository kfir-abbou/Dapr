using Microsoft.AspNetCore.Mvc;
using ServiceB.Models;
using ServiceB.Stores;

namespace ServiceB.Endpoints;

public static class ItemEndpoints
{
    public static WebApplication MapItemEndpoints(this WebApplication app)
    {
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

        return app;
    }
}
