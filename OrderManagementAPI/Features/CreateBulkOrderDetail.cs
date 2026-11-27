using FluentValidation;
using MediatR;
using OrderManagement;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public static class CreateBulkOrderDetail
{
    public sealed record Item(Guid ProductId, int Quantity, decimal Discount, string? Notes);

    public sealed record Command(
        Guid OrderId,
        IReadOnlyList<Item> Items
    ) : IRequest<IReadOnlyList<OrderDetailResponseDto>>;
    
    public sealed class Validator: AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId).NotEmpty();
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.Quantity).GreaterThan(0);
                item.RuleFor(j => j.Discount).GreaterThan(0);
            });
            RuleFor(x => x.Items)
                .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
                .WithMessage("Duplicate products in request");
        }
    }

    public sealed class Handler : IRequestHandler<Command, IReadOnlyList<OrderDetailResponseDto>>
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

        public async Task<IReadOnlyList<OrderDetailResponseDto>> Handle(Command request, CancellationToken ct)
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);

            var order = await _orders.GetByIdAsync(request.OrderId, ct);
            EnsureOrderEditable(order, request.OrderId);

            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var products = await _products.GetByIdsForUpdateAsync(productIds, ct);

            ValidateProductItem(request.Items, products);

            var details = CreateOrderDetails(request.OrderId, request.Items, products);
            _details.AddRange(details);
            await _uow.SaveChangesAsync(ct);

            await _orders.RecalculateTotalAsync(request.OrderId, ct);
            await tx.CommitAsync();

            return await _details.GetDtosByIdsAsync(details.Select(d => d.Id).ToList(), ct);
        }

        private void ValidateProductItem(IReadOnlyList<Item> items, IReadOnlyDictionary<Guid, Product> products)
        {
            var errors = new List<BulkItemError>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!products.TryGetValue(item.ProductId, out var product))
                    errors.Add(new BulkItemError(i, item.ProductId, "Product not found"));
                else if (item.Quantity > product.Stock)
                    errors.Add(new BulkItemError(i, item.ProductId,
                        $"Insufficient stock: requested {item.Quantity}, available {product.Stock}"));
                else if (item.Discount > product.Price * item.Quantity)
                    errors.Add(new BulkItemError(i, item.ProductId, "Discount cannot exceed unitPrice × quantity"));
            }

            if (errors.Count > 0)
                throw new BadRequestException($"{errors.Count} of {items.Count} items are invalid, nothing was created")
                {
                    Errors = errors
                };
        }

        private void EnsureOrderEditable(Order? order, Guid orderId)
        {
            if (order is null)
                throw new NotFoundException($"Order '{orderId}' not found");
            if (!order.IsEditable())
                throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot add items");
        }

        private IReadOnlyList<OrderDetail> CreateOrderDetails(Guid orderId, IReadOnlyList<Item> items,
            IReadOnlyDictionary<Guid, Product> products)
        {
            var details = new List<OrderDetail>(items.Count);
            foreach (var item in items)
            {
                var product = products[item.ProductId];
                product.DecreaseStock(item.Quantity);
                details.Add(new OrderDetail(orderId, product.Id, item.Quantity, product.Price, item.Discount, item.Notes));
            }

            return details;
        }
    }
}
