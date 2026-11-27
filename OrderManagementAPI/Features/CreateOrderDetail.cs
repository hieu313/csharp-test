using FluentValidation;
using MediatR;
using OrderManagement;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public static class CreateOrderDetail
{
    public sealed record Command(
        Guid OrderId,
        Guid ProductId,
        int Quantity,
        decimal Discount,
        string? Notes
    ) : IRequest<OrderDetailResponseDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId).NotEmpty();
            RuleFor(x => x.ProductId).NotEmpty();
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleFor(x => x.Discount).GreaterThanOrEqualTo(0);
        }
    }

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

            var order = await _orders.GetByIdAsync(request.OrderId, ct);
            EnsureOrderEditable(order, request.OrderId);

            var products = await _products.GetByIdsForUpdateAsync([request.ProductId], ct);
            var product = ValidateProduct(request, products);

            product.DecreaseStock(request.Quantity);
            var detail = new OrderDetail(request.OrderId, product.Id, request.Quantity, product.Price, request.Discount, request.Notes);
            _details.Add(detail);
            await _uow.SaveChangesAsync(ct);

            await _orders.RecalculateTotalAsync(request.OrderId, ct);
            await tx.CommitAsync();

            return (await _details.GetDtoByIdAsync(detail.Id, ct))!;
        }

        private static Product ValidateProduct(Command request, IReadOnlyDictionary<Guid, Product> products)
        {
            if (!products.TryGetValue(request.ProductId, out var product))
                throw new NotFoundException($"Product '{request.ProductId}' not found");

            if (request.Quantity > product.Stock)
                throw new ConflictException($"Insufficient stock: requested {request.Quantity}, available {product.Stock}");

            if (request.Discount > product.Price * request.Quantity)
                throw new BadRequestException("Discount cannot exceed unitPrice × quantity");

            return product;
        }

        private static void EnsureOrderEditable(Order? order, Guid orderId)
        {
            if (order is null)
                throw new NotFoundException($"Order '{orderId}' not found");
            if (!order.IsEditable())
                throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot add items");
        }
    }
}
