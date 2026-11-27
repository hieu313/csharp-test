using MediatR;
using OrderManagement.Dtos;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Queries;

public sealed class GetOrderDetailListHandler : IRequestHandler<GetOrderDetailListQuery, PagedResponse<OrderDetailResponseDto>>
{
    private readonly IOrderDetailRepository _details;

    public GetOrderDetailListHandler(IOrderDetailRepository details)
        => _details = details;

    public async Task<PagedResponse<OrderDetailResponseDto>> Handle(GetOrderDetailListQuery request, CancellationToken ct)
    {
        var (items, total) = await _details.GetPagedDtosAsync(request.OrderId, request.ProductId, request.Page, request.Limit);
        var totalPages = (int)Math.Ceiling(total / (double)request.Limit);

        return new PagedResponse<OrderDetailResponseDto>(
            items,
            new PaginationMeta(request.Page, request.Limit, total, totalPages));
    }
}
