using MediatR;
using OrderManagement;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Commands;

public class CreateOrderDetailHandler : IRequestHandler<CreateOrderDetailCommand, OrderDetailResponseDto>
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

    public async Task<OrderDetailResponseDto> Handle(CreateOrderDetailCommand request, CancellationToken ct)
    {
        await using var tx = await _uow.BeginTransactionAsync();

        await EnsureOrderEditableAsync(request.OrderId);

        var product = await _products.GetByIdAsync(request.ProductId)
            ?? throw new NotFoundException($"Product '{request.ProductId}' not found");

        var unitPrice = product.Price;

        if (request.Discount > unitPrice * request.Quantity)
            throw new BadRequestException("Discount cannot exceed unitPrice × quantity");

        var isStockDecreased = await _products.TryDecreaseStockAsync(request.ProductId, request.Quantity);

        if (!isStockDecreased)
        {
            var available = await _products.GetStockAsync(request.ProductId)
                            ?? throw new NotFoundException($"Product '{request.ProductId}' not found");
            throw new ConflictException($"Insufficient stock: requested {request.Quantity}, available {available}");
        }

        var detail = new OrderDetail(request.OrderId, request.ProductId, request.Quantity, unitPrice, request.Discount, request.Notes);
        _orderDetails.Add(detail);
        await _uow.SaveChangesAsync();

        await _orders.RecalculateTotalAsync(request.OrderId);
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