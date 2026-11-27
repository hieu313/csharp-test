using MediatR;
using OrderManagement;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public class CreateOrderDetail
{
    public sealed record Command(
        Guid OrderId,
        Guid ProductId,
        int Quantity,
        decimal Discount,
        string? Notes
    ) : IRequest<OrderDetailResponseDto>;

    public sealed class Handler : IRequestHandler<Command, OrderDetailResponseDto>
    {
        private readonly IOrderDetailRepository _details;
        private readonly IProductRepository _products;
        private readonly IOrderRepository _orders;
        private readonly IUnitOfWork _uow;

        public Handler(
            IOrderDetailRepository details,
            IProductRepository products,
            IOrderRepository orders,
            IUnitOfWork uow)
        {
            _details = details;
            _products = products;
            _orders = orders;
            _uow = uow;
        }

        public async Task<OrderDetailResponseDto> Handle(Command request, CancellationToken ct)
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);

            await EnsureOrderEditableAsync(request.OrderId, ct);

            var product = await _products.GetByIdAsync(request.ProductId, ct)
                ?? throw new NotFoundException($"Product '{request.ProductId}' not found");

            var unitPrice = product.Price;

            if (request.Discount > unitPrice * request.Quantity)
                throw new BadRequestException("Discount cannot exceed unitPrice × quantity");

            if (!await _products.TryDecreaseStockAsync(request.ProductId, request.Quantity, ct))
            {
                var available = await _products.GetStockAsync(request.ProductId, ct)
                    ?? throw new NotFoundException($"Product '{request.ProductId}' not found");
                throw new ConflictException($"Insufficient stock: requested {request.Quantity}, available {available}");
            }

            var detail = new OrderDetail(request.OrderId, request.ProductId, request.Quantity, unitPrice, request.Discount, request.Notes);
            _details.Add(detail);
            await _uow.SaveChangesAsync(ct);

            await _orders.RecalculateTotalAsync(request.OrderId, ct);
            await tx.CommitAsync();

            return (await _details.GetDtoByIdAsync(detail.Id, ct))!;
        }

        private async Task EnsureOrderEditableAsync(Guid orderId, CancellationToken ct)
        {
            var order = await _orders.GetByIdAsync(orderId, ct)
                ?? throw new NotFoundException($"Order '{orderId}' not found");

            if (!order.IsEditable())
                throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot add items");
        }
    }
}
