using FluentValidation;
using MediatR;
using OrderManagement;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public static class UpdateOrderDetail
{
    public sealed record Command(
        Guid Id,
        int? Quantity,
        decimal? Discount,
        string? Notes
    ) : IRequest<OrderDetailResponseDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity.HasValue);
            RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).When(x => x.Discount.HasValue);
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

            var detail = await _details.GetForUpdateAsync(request.Id, ct)
                ?? throw new NotFoundException($"Order detail '{request.Id}' not found");

            var order = await _orders.GetByIdAsync(detail.OrderId, ct);
            EnsureOrderEditable(order, detail.OrderId);

            var newQuantity = request.Quantity ?? detail.Quantity;
            var newDiscount = request.Discount ?? detail.Discount;

            if (newDiscount > detail.UnitPrice * newQuantity)
                throw new BadRequestException("Discount cannot exceed unitPrice × quantity");

            var delta = newQuantity - detail.Quantity;
            if (delta != 0)
            {
                var products = await _products.GetByIdsForUpdateAsync([detail.ProductId], ct);
                if (!products.TryGetValue(detail.ProductId, out var product))
                    throw new NotFoundException($"Product '{detail.ProductId}' not found");

                if (delta > 0)
                {
                    if (delta > product.Stock)
                        throw new ConflictException($"Insufficient stock: requested {delta} more, available {product.Stock}");
                    product.DecreaseStock(delta);
                }
                else
                {
                    product.IncreaseStock(-delta);
                }
            }

            detail.Update(request.Quantity, request.Discount, request.Notes);
            await _uow.SaveChangesAsync(ct);

            await _orders.RecalculateTotalAsync(detail.OrderId, ct);
            await tx.CommitAsync();

            return (await _details.GetDtoByIdAsync(request.Id, ct))!;
        }

        private static void EnsureOrderEditable(Order? order, Guid orderId)
        {
            if (order is null)
                throw new NotFoundException($"Order '{orderId}' not found");
            if (!order.IsEditable())
                throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot modify items");
        }
    }
}
