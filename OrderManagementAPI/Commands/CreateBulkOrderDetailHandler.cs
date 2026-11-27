using OrderManagement;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Commands;

public sealed class CreateBulkOrderDetailHandler
{
    private readonly IOrderDetailRepository _details;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;

    public CreateBulkOrderDetailHandler(
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

    public async Task<IReadOnlyList<OrderDetailResponseDto>> HandleAsync(CreateBulkOrderDetailCommand cmd)
    {
        var duplicates = cmd.Items
            .GroupBy(i => i.ProductId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new BadRequestException($"Duplicate products in request: {string.Join(", ", duplicates)}");

        await using var tx = await _uow.BeginTransactionAsync();

        var order = await _orders.GetByIdAsync(cmd.OrderId)
            ?? throw new NotFoundException($"Order '{cmd.OrderId}' not found");

        if (!order.IsEditable())
            throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot add items");

        var productIds = cmd.Items.Select(i => i.ProductId).ToList();
        var products = await _products.GetByIdsForUpdateAsync(productIds);

        var errors = new List<BulkItemError>();
        for (var i = 0; i < cmd.Items.Count; i++)
        {
            var item = cmd.Items[i];
            if (!products.TryGetValue(item.ProductId, out var product))
                errors.Add(new BulkItemError(i, item.ProductId, "Product not found"));
            else if (item.Quantity > product.Stock)
                errors.Add(new BulkItemError(i, item.ProductId,
                    $"Insufficient stock: requested {item.Quantity}, available {product.Stock}"));
            else if (item.Discount > product.Price * item.Quantity)
                errors.Add(new BulkItemError(i, item.ProductId, "Discount cannot exceed unitPrice × quantity"));
        }

        if (errors.Count > 0)
            throw new BadRequestException($"{errors.Count} of {cmd.Items.Count} items are invalid, nothing was created")
            {
                Errors = errors
            };

        var details = new List<OrderDetail>(cmd.Items.Count);
        foreach (var item in cmd.Items)
        {
            var product = products[item.ProductId];
            product.DecreaseStock(item.Quantity);
            details.Add(new OrderDetail(cmd.OrderId, product.Id, item.Quantity, product.Price, item.Discount, item.Notes));
        }
        _details.AddRange(details);
        await _uow.SaveChangesAsync();

        await _orders.RecalculateTotalAsync(cmd.OrderId);
        await tx.CommitAsync();

        return await _details.GetDtosByIdsAsync(details.Select(d => d.Id).ToList());
    }
}
