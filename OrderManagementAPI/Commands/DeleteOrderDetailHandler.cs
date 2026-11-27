using MediatR;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Commands;

public sealed class DeleteOrderDetailHandler : IRequestHandler<DeleteOrderDetailCommand>
{
    private readonly IOrderDetailRepository _details;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;

    public DeleteOrderDetailHandler(
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

    public async Task Handle(DeleteOrderDetailCommand request, CancellationToken ct)
    {
        await using var tx = await _uow.BeginTransactionAsync();

        var detail = await _details.GetForUpdateAsync(request.Id)
            ?? throw new NotFoundException($"Order detail '{request.Id}' not found");

        var order = await _orders.GetByIdAsync(detail.OrderId)
            ?? throw new NotFoundException($"Order '{detail.OrderId}' not found");

        if (!order.IsEditable())
            throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot remove items");

        await _products.IncreaseStockAsync(detail.ProductId, detail.Quantity);

        _details.Remove(detail);
        await _uow.SaveChangesAsync();

        await _orders.RecalculateTotalAsync(detail.OrderId);
        await tx.CommitAsync();
    }
}
