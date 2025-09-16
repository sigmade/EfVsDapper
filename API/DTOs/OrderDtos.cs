namespace API.DTOs;

public record OrderItemDto(int Id, int ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record OrderDto(int Id, DateTime CreatedAt, int UserId, string UserName, decimal Total, List<OrderItemDto> Items);

public record OrderListResponse(
    string Mode, // EF or Dapper
    List<OrderDto> Orders,
    long ElapsedMs
);
