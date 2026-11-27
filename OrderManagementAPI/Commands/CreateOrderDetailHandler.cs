using OrderManagement;
using OrderManagement.Dtos;
using OrderManagement.Repositories;
using OrderManagement.Exceptions;

namespace OrderManagementAPI.Commands;

public class CreateOrderDetailHandler
{
    private readonly IOrderDetailRepository _orderDetails;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;

    public CreateOrderDetailHandler(
        IOrderDetailRepository orderDetails,
        IProductRepository products,
        IOrderRepository orders,
        IUnitOfWork uow)
    {
        _orderDetails = orderDetails;
        _products = products;
        _orders = orders;
        _uow = uow;
    }

    public async Task<OrderDetailResponseDto> HandleAsync(CreateOrderDetailCommand cmd)
    {
        await using var tx = await _uow.BeginTransactionAsync();
        
        await EnsureOrderEditableAsync(cmd.OrderId);
        
        var product = await _products.GetByIdAsync(cmd.ProductId)
            ?? throw new NotFoundException($"Product '{cmd.ProductId}' not found");

        var unitPrice = product.Price;
        
        if (cmd.Discount > unitPrice * cmd.Quantity)
            throw new BadRequestException("Discount cannot exceed unitPrice × quantity");

        var isStockDecreased = await _products.TryDecreaseStockAsync(cmd.ProductId, cmd.Quantity);
        
        if (!isStockDecreased)
        {
            var available = await _products.GetStockAsync(cmd.ProductId)
                            ?? throw new NotFoundException($"Product '{cmd.ProductId}' not found");
            throw new ConflictException($"Insufficient stock: requested {cmd.Quantity}, available {available}");
        }
        
        var detail = new OrderDetail(cmd.OrderId, cmd.ProductId, cmd.Quantity, unitPrice, cmd.Discount, cmd.Notes);
        _orderDetails.Add(detail);
        await _uow.SaveChangesAsync();

        await _orders.RecalculateTotalAsync(cmd.OrderId);
        await tx.CommitAsync();

        return (await _orderDetails.GetDtoByIdAsync(detail.Id))!;
    }
    
    private async Task EnsureOrderEditableAsync(Guid orderId)
    {
        var order = await _orders.GetByIdAsync(orderId)
                    ?? throw new NotFoundException($"Order '{orderId}' not found");

        if (!order.IsEditable())
            throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot add items");
    } 
}