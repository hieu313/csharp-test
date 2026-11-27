using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Queries;

public sealed class GetOrderDetailsByOrderHandler
{
    private readonly IOrderDetailRepository _details;
    private readonly IOrderRepository _orders;

    public GetOrderDetailsByOrderHandler(IOrderDetailRepository details, IOrderRepository orders)
    {
        _details = details;
        _orders = orders;
    }

    public async Task<OrderDetailsByOrderResponse> HandleAsync(GetOrderDetailsByOrderQuery query)
    {
        if (!await _orders.ExistsAsync(query.OrderId))
            throw new NotFoundException($"Order '{query.OrderId}' not found");

        var data = await _details.GetDtosByOrderIdAsync(query.OrderId);

        var summary = new OrderSummary(
            TotalItems: data.Count,
            TotalQuantity: data.Sum(d => d.Quantity),
            TotalAmount: data.Sum(d => d.Subtotal));

        return new OrderDetailsByOrderResponse(data, summary);
    }
}
