using System.Data;
using API.Data;
using API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dapper;

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
            var sql = @"SELECT o.id as OrderId, o.createdat, o.userid, u.name as username,
                                oi.id as OrderItemId, oi.productid, p.name as productname, oi.quantity, oi.unitprice
                         FROM orders o
                         JOIN users u ON u.id = o.userid
                         JOIN orderitems oi ON oi.orderid = o.id
                         JOIN products p ON p.id = oi.productid
                         ORDER BY o.createdat DESC
                         LIMIT 10";

            var lookup = new Dictionary<int, OrderDto>();

            var rows = await _conn.QueryAsync(sql);
            foreach (var r in rows)
            {
                int orderId = r.orderid;
                if (!lookup.TryGetValue(orderId, out var dto))
                {
                    dto = new OrderDto(orderId, (DateTime)r.createdat, (int)r.userid, (string)r.username, 0m, new List<OrderItemDto>());
                    lookup[orderId] = dto;
                }
                var item = new OrderItemDto((int)r.orderitemid, (int)r.productid, (string)r.productname, (int)r.quantity, (decimal)r.unitprice);
                dto.Items.Add(item);
            }
            foreach (var dto in lookup.Values)
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
