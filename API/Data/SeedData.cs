using API.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return; // Already seeded

        var random = new Random();

        var categories = Enumerable.Range(1, 5).Select(i => new Category { Name = $"Category {i}" }).ToList();
        var products = Enumerable.Range(1, 30).Select(i => new Product
        {
            Name = $"Product {i}",
            Price = (decimal)(random.NextDouble() * 100 + 1),
            Category = categories[random.Next(categories.Count)]
        }).ToList();
        var users = Enumerable.Range(1, 20).Select(i => new User
        {
            Name = $"User {i}",
            Email = $"user{i}@example.com"
        }).ToList();

        var orders = new List<Order>();
        var orderItems = new List<OrderItem>();

        for (int i = 0; i < 100; i++)
        {
            var user = users[random.Next(users.Count)];
            var order = new Order
            {
                CreatedAt = DateTime.UtcNow.AddMinutes(-random.Next(0, 60 * 24 * 30)),
                User = user
            };
            int itemsCount = random.Next(1, 6);
            for (int j = 0; j < itemsCount; j++)
            {
                var product = products[random.Next(products.Count)];
                var quantity = random.Next(1, 5);
                order.Items.Add(new OrderItem
                {
                    Product = product,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
            }
            orders.Add(order);
        }

        await db.AddRangeAsync(categories);
        await db.AddRangeAsync(products);
        await db.AddRangeAsync(users);
        await db.AddRangeAsync(orders);

        await db.SaveChangesAsync();
    }
}
