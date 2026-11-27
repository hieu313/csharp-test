using MediatR;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public class DeleteOrderDetail
{
    public sealed record Command(Guid Id) : IRequest;

    public sealed class Handler : IRequestHandler<Command>
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

        public async Task Handle(Command request, CancellationToken ct)
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);

            var detail = await _details.GetForUpdateAsync(request.Id, ct)
                ?? throw new NotFoundException($"Order detail '{request.Id}' not found");

            var order = await _orders.GetByIdAsync(detail.OrderId, ct)
                ?? throw new NotFoundException($"Order '{detail.OrderId}' not found");

            if (!order.IsEditable())
                throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot remove items");

            await _products.IncreaseStockAsync(detail.ProductId, detail.Quantity, ct);

            _details.Remove(detail);
            await _uow.SaveChangesAsync(ct);

            await _orders.RecalculateTotalAsync(detail.OrderId, ct);
            await tx.CommitAsync();
        }
    }
}
