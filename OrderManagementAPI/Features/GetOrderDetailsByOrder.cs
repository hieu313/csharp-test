using MediatR;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public class GetOrderDetailsByOrder
{
    public sealed record Query(Guid OrderId) : IRequest<OrderDetailsByOrderResponse>;

    public sealed class Handler : IRequestHandler<Query, OrderDetailsByOrderResponse>
    {
        private readonly IOrderDetailRepository _details;
        private readonly IOrderRepository _orders;

        public Handler(IOrderDetailRepository details, IOrderRepository orders)
        {
            _details = details;
            _orders = orders;
        }

        public async Task<OrderDetailsByOrderResponse> Handle(Query request, CancellationToken ct)
        {
            if (!await _orders.ExistsAsync(request.OrderId))
                throw new NotFoundException($"Order '{request.OrderId}' not found");

            var data = await _details.GetDtosByOrderIdAsync(request.OrderId);

            var summary = new OrderSummary(
                TotalItems: data.Count,
                TotalQuantity: data.Sum(d => d.Quantity),
                TotalAmount: data.Sum(d => d.Subtotal));

            return new OrderDetailsByOrderResponse(data, summary);
        }
    }
}
