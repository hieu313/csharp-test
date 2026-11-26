using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagement.Services;

public interface IOrderDetailService
{
    Task<OrderDetailResponseDto?> GetByIdAsync(Guid id);
    Task<PagedResponse<OrderDetailResponseDto>> GetListAsync(OrderDetailQuery q);
    Task<OrderDetailResponseDto> CreateAsync(CreateOrderDetailRequest req);
    Task<IReadOnlyList<OrderDetailResponseDto>> CreateBulkAsync(CreateBulkOrderDetailRequest req);
    Task<OrderDetailResponseDto> UpdateAsync(Guid id, UpdateOrderDetailRequest req);
    Task DeleteAsync(Guid id);
    Task<OrderDetailsByOrderResponse> GetByOrderIdAsync(Guid orderId);
    Task<OrderDetailsByProductResponse> GetByProductIdAsync(Guid productId);
}

public class OrderDetailService : IOrderDetailService
{
    private readonly IOrderDetailRepository _details;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;

    public OrderDetailService(
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


    public Task<OrderDetailResponseDto?> GetByIdAsync(Guid id) => _details.GetDtoByIdAsync(id);

    public async Task<PagedResponse<OrderDetailResponseDto>> GetListAsync(OrderDetailQuery q)
    {
        var (items, total) = await _details.GetPagedDtosAsync(q.OrderId, q.ProductId, q.Page, q.Limit);
        var totalPages = (int)Math.Ceiling(total / (double)q.Limit);   // làm tròn lên

        return new PagedResponse<OrderDetailResponseDto>(
            items,
            new PaginationMeta(q.Page, q.Limit, total, totalPages));
    }

    public async Task<OrderDetailsByOrderResponse> GetByOrderIdAsync(Guid orderId)
    {
        if (!await _orders.ExistsAsync(orderId))
            throw new NotFoundException($"Order '{orderId}' not found");

        var data = await _details.GetDtosByOrderIdAsync(orderId);

        var summary = new OrderSummary(
            TotalItems: data.Count,
            TotalQuantity: data.Sum(d => d.Quantity),
            TotalAmount: data.Sum(d => d.Subtotal));

        return new OrderDetailsByOrderResponse(data, summary);
    }

    public async Task<OrderDetailsByProductResponse> GetByProductIdAsync(Guid productId)
    {
        if (!await _products.ExistsAsync(productId))
            throw new NotFoundException($"Product '{productId}' not found");

        var data = await _details.GetDtosByProductIdAsync(productId);

        // Summary bỏ đơn cancelled (hủy thì không phải "đã bán") → khác điều kiện với data,
        // nên để DB tính bằng 1 câu aggregate
        var summary = await _details.GetSalesSummaryByProductAsync(productId);

        return new OrderDetailsByProductResponse(data, summary);
    }

    public async Task<OrderDetailResponseDto> CreateAsync(CreateOrderDetailRequest req)
    {
        var orderId = req.OrderId!.Value;
        var productId = req.ProductId!.Value;
        var quantity = req.Quantity!.Value;
        var discount = req.Discount ?? 0;

        await using var tx = await _uow.BeginTransactionAsync();

        await EnsureOrderEditableAsync(orderId, "add items");

        var product = await _products.GetByIdAsync(productId)
            ?? throw new NotFoundException($"Product '{productId}' not found");
        var unitPrice = product.Price;// chốt giá tại thời điểm đặt

        EnsureDiscountValid(discount, unitPrice, quantity);
        await DecreaseStockOrThrowAsync(productId, quantity);

        var detail = new OrderDetail(orderId, productId, quantity, unitPrice, discount, req.Notes);
        _details.Add(detail);
        await _uow.SaveChangesAsync();

        await _orders.RecalculateTotalAsync(orderId);
        await tx.CommitAsync();

        return (await _details.GetDtoByIdAsync(detail.Id))!;
    }

    public async Task<IReadOnlyList<OrderDetailResponseDto>> CreateBulkAsync(CreateBulkOrderDetailRequest req)
    {
        var orderId = req.OrderId!.Value;
        var items = req.Items;

        var duplicates = items
            .GroupBy(i => i.ProductId!.Value)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new BadRequestException($"Duplicate products in request: {string.Join(", ", duplicates)}");

        await using var tx = await _uow.BeginTransactionAsync();

        await EnsureOrderEditableAsync(orderId, "add items");

        var productIds = items.Select(i => i.ProductId!.Value).ToList();
        var products = await _products.GetByIdsForUpdateAsync(productIds);

        var errors = new List<BulkItemError>();
        for (var i = 0; i < items.Count; i++)
        {
            var productId = items[i].ProductId!.Value;
            var quantity = items[i].Quantity!.Value;
            var discount = items[i].Discount ?? 0;

            if (!products.TryGetValue(productId, out var product))
                errors.Add(new BulkItemError(i, productId, "Product not found"));
            else if (quantity > product.Stock)
                errors.Add(new BulkItemError(i, productId,
                    $"Insufficient stock: requested {quantity}, available {product.Stock}"));
            else if (discount > product.Price * quantity)
                errors.Add(new BulkItemError(i, productId, "Discount cannot exceed unitPrice × quantity"));
        }

        if (errors.Count > 0)
            throw new BadRequestException($"{errors.Count} of {items.Count} items are invalid, nothing was created")
            {
                Errors = errors
            };

        var details = new List<OrderDetail>(items.Count);
        foreach (var item in items)
        {
            var product = products[item.ProductId!.Value];
            var quantity = item.Quantity!.Value;

            product.DecreaseStock(quantity);
            details.Add(new OrderDetail(orderId, product.Id, quantity, product.Price, item.Discount ?? 0));
        }
        _details.AddRange(details);
        await _uow.SaveChangesAsync();

        await _orders.RecalculateTotalAsync(orderId);
        await tx.CommitAsync();

        return await _details.GetDtosByIdsAsync(details.Select(d => d.Id).ToList());
    }

    public async Task<OrderDetailResponseDto> UpdateAsync(Guid id, UpdateOrderDetailRequest req)
    {
        await using var tx = await _uow.BeginTransactionAsync();

        var detail = await GetDetailForUpdateOrThrowAsync(id);
        await EnsureOrderEditableAsync(detail.OrderId, "modify items");

        var newQuantity = req.Quantity ?? detail.Quantity;
        var newDiscount = req.Discount ?? detail.Discount;

        EnsureDiscountValid(newDiscount, detail.UnitPrice, newQuantity);

        // Chỉ điều chỉnh stock theo phần chênh lệch
        var delta = newQuantity - detail.Quantity;
        if (delta > 0)
            await DecreaseStockOrThrowAsync(detail.ProductId, delta);   // lấy thêm hàng
        else if (delta < 0)
            await _products.IncreaseStockAsync(detail.ProductId, -delta); // hoàn trả hàng

        detail.Update(req.Quantity, req.Discount, req.Notes);   // entity tự tính lại subtotal
        await _uow.SaveChangesAsync();

        await _orders.RecalculateTotalAsync(detail.OrderId);
        await tx.CommitAsync();

        return (await _details.GetDtoByIdAsync(id))!;
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var tx = await _uow.BeginTransactionAsync();

        var detail = await GetDetailForUpdateOrThrowAsync(id);
        await EnsureOrderEditableAsync(detail.OrderId, "remove items");

        await _products.IncreaseStockAsync(detail.ProductId, detail.Quantity);   // hoàn trả stock

        _details.Remove(detail);
        await _uow.SaveChangesAsync();

        await _orders.RecalculateTotalAsync(detail.OrderId);
        await tx.CommitAsync();
    }

    private async Task<OrderDetail> GetDetailForUpdateOrThrowAsync(Guid id)
        => await _details.GetForUpdateAsync(id)
           ?? throw new NotFoundException($"Order detail '{id}' not found");

    private async Task EnsureOrderEditableAsync(Guid orderId, string action)
    {
        var order = await _orders.GetByIdAsync(orderId)
            ?? throw new NotFoundException($"Order '{orderId}' not found");

        if (!order.IsEditable())
            throw new ConflictException($"Order is {order.Status.ToString().ToLower()}, cannot {action}");
    }

    private static void EnsureDiscountValid(decimal discount, decimal unitPrice, int quantity)
    {
        if (discount > unitPrice * quantity)
            throw new BadRequestException("Discount cannot exceed unitPrice × quantity");
    }

    private async Task DecreaseStockOrThrowAsync(Guid productId, int quantity)
    {
        if (await _products.TryDecreaseStockAsync(productId, quantity))
            return;

        //phân biệt "không tồn tại" và "không đủ hàng" để báo lỗi rõ ràng
        var available = await _products.GetStockAsync(productId)
            ?? throw new NotFoundException($"Product '{productId}' not found");
        throw new ConflictException($"Insufficient stock: requested {quantity}, available {available}");
    }
}
