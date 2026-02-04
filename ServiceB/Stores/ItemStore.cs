using ServiceB.Models;

namespace ServiceB.Stores;

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
