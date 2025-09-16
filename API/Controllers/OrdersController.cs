using API.Data;
using API.DTOs;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDbConnection _conn;
    public OrdersController(AppDbContext db, IDbConnection conn)
    {
        _db = db;
        _conn = conn;
    }

    [HttpGet("top10")] // /api/orders/top10?mode=ef or dapper
    public async Task<ActionResult<OrderListResponse>> GetTop10([FromQuery] string mode = "ef")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (mode.Equals("dapper", StringComparison.OrdinalIgnoreCase))
        {
            var sql = @" SELECT o.""Id""          AS ""OrderId"", o.""CreatedAt""   AS ""CreatedAt"", o.""UserId""      AS ""UserId"", u.""Name""        AS ""UserName"", oi.""Id""         AS ""OrderItemId"", oi.""ProductId""  AS ""ProductId"", p.""Name""        AS ""ProductName"", oi.""Quantity""   AS ""Quantity"", oi.""UnitPrice""  AS ""UnitPrice"" FROM ""Orders"" o JOIN ""Users"" u       ON u.""Id"" = o.""UserId"" JOIN ""OrderItems"" oi ON oi.""OrderId"" = o.""Id"" JOIN ""Products"" p    ON p.""Id"" = oi.""ProductId"" ORDER BY o.""CreatedAt"" DESC LIMIT 10";

            var lookup = new Dictionary<int, OrderDto>();

            var rows = await _conn.QueryAsync(sql);
            foreach (var r in rows)
            {
                // Fix property name casing to match SQL aliases
                var orderId = (int)r.OrderId;
                if (!lookup.TryGetValue(orderId, out var dto))
                {
                    dto = new OrderDto(orderId, (DateTime)r.CreatedAt, (int)r.UserId, (string)r.UserName, 0m, new List<OrderItemDto>());
                    lookup[orderId] = dto;
                }
                var item = new OrderItemDto((int)r.OrderItemId, (int)r.ProductId, (string)r.ProductName, (int)r.Quantity, (decimal)r.UnitPrice);
                dto.Items.Add(item);
            }
            foreach (var dto in lookup.Values.ToList())
            {
                dto.Items.TrimExcess();
                var total = dto.Items.Sum(i => i.Quantity * i.UnitPrice);
                // record is immutable, create new instance
                lookup[dto.Id] = dto with { Total = total };
            }
            sw.Stop();
            return Ok(new OrderListResponse("dapper", lookup.Values.ToList(), sw.ElapsedMilliseconds));
        }
        else
        {
            var orders = await _db.Orders
                .Include(o => o.User)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .AsNoTracking()
                .Select(o => new OrderDto(
                    o.Id,
                    o.CreatedAt,
                    o.UserId,
                    o.User!.Name,
                    o.Items.Sum(i => i.Quantity * i.UnitPrice),
                    o.Items.Select(i => new OrderItemDto(i.Id, i.ProductId, i.Product!.Name, i.Quantity, i.UnitPrice)).ToList()
                ))
                .ToListAsync();
            sw.Stop();
            return Ok(new OrderListResponse("ef", orders, sw.ElapsedMilliseconds));
        }
    }
}
