using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Commands;

public sealed class UpdateOrderDetailHandler
{
    private readonly IOrderDetailRepository _details;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;

    public UpdateOrderDetailHandler(
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

    public async Task<OrderDetailResponseDto> HandleAsync(UpdateOrderDetailCommand cmd)
    {
        await using var tx = await _uow.BeginTransactionAsync();

        var detail = await _details.GetForUpdateAsync(cmd.Id)
            ?? throw new NotFoundException($"Order detail '{cmd.Id}' not found");

        var order = await _orders.GetByIdAsync(detail.OrderId)
            ?? throw new NotFoundException($"Order '{detail.OrderId}' not found");

        if (!order.IsEditable())
            throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot modify items");

        var newQuantity = cmd.Quantity ?? detail.Quantity;
        var newDiscount = cmd.Discount ?? detail.Discount;

        if (newDiscount > detail.UnitPrice * newQuantity)
            throw new BadRequestException("Discount cannot exceed unitPrice × quantity");

        var delta = newQuantity - detail.Quantity;
        if (delta > 0)
        {
            if (!await _products.TryDecreaseStockAsync(detail.ProductId, delta))
            {
                var available = await _products.GetStockAsync(detail.ProductId)
                    ?? throw new NotFoundException($"Product '{detail.ProductId}' not found");
                throw new ConflictException($"Insufficient stock: requested {delta} more, available {available}");
            }
        }
        else if (delta < 0)
        {
            await _products.IncreaseStockAsync(detail.ProductId, -delta);
        }

        detail.Update(cmd.Quantity, cmd.Discount, cmd.Notes);
        await _uow.SaveChangesAsync();

        await _orders.RecalculateTotalAsync(detail.OrderId);
        await tx.CommitAsync();

        return (await _details.GetDtoByIdAsync(cmd.Id))!;
    }
}
