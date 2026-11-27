using OrderManagement.Dtos;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Queries;

public sealed class GetOrderDetailListHandler
{
    private readonly IOrderDetailRepository _details;

    public GetOrderDetailListHandler(IOrderDetailRepository details)
        => _details = details;

    public async Task<PagedResponse<OrderDetailResponseDto>> HandleAsync(GetOrderDetailListQuery query)
    {
        var (items, total) = await _details.GetPagedDtosAsync(query.OrderId, query.ProductId, query.Page, query.Limit);
        var totalPages = (int)Math.Ceiling(total / (double)query.Limit);

        return new PagedResponse<OrderDetailResponseDto>(
            items,
            new PaginationMeta(query.Page, query.Limit, total, totalPages));
    }
}
