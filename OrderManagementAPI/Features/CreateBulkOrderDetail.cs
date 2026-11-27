using MediatR;
using OrderManagement;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public class CreateBulkOrderDetail
{
    public sealed record Item(Guid ProductId, int Quantity, decimal Discount, string? Notes);

    public sealed record Command(
        Guid OrderId,
        IReadOnlyList<Item> Items
    ) : IRequest<IReadOnlyList<OrderDetailResponseDto>>;

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
            var duplicates = request.Items
                .GroupBy(i => i.ProductId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                throw new BadRequestException($"Duplicate products in request: {string.Join(", ", duplicates)}");

            await using var tx = await _uow.BeginTransactionAsync();

            var order = await _orders.GetByIdAsync(request.OrderId)
                ?? throw new NotFoundException($"Order '{request.OrderId}' not found");

            if (!order.IsEditable())
                throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot add items");

            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var products = await _products.GetByIdsForUpdateAsync(productIds);

            var errors = new List<BulkItemError>();
            for (var i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                if (!products.TryGetValue(item.ProductId, out var product))
                    errors.Add(new BulkItemError(i, item.ProductId, "Product not found"));
                else if (item.Quantity > product.Stock)
                    errors.Add(new BulkItemError(i, item.ProductId,
                        $"Insufficient stock: requested {item.Quantity}, available {product.Stock}"));
                else if (item.Discount > product.Price * item.Quantity)
                    errors.Add(new BulkItemError(i, item.ProductId, "Discount cannot exceed unitPrice × quantity"));
            }

            if (errors.Count > 0)
                throw new BadRequestException($"{errors.Count} of {request.Items.Count} items are invalid, nothing was created")
                {
                    Errors = errors
                };

            var details = new List<OrderDetail>(request.Items.Count);
            foreach (var item in request.Items)
            {
                var product = products[item.ProductId];
                product.DecreaseStock(item.Quantity);
                details.Add(new OrderDetail(request.OrderId, product.Id, item.Quantity, product.Price, item.Discount, item.Notes));
            }
            _details.AddRange(details);
            await _uow.SaveChangesAsync();

            await _orders.RecalculateTotalAsync(request.OrderId);
            await tx.CommitAsync();

            return await _details.GetDtosByIdsAsync(details.Select(d => d.Id).ToList());
        }
    }
}
