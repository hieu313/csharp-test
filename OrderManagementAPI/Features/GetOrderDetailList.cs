using MediatR;
using OrderManagement.Dtos;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public class GetOrderDetailList
{
    public sealed record Query(Guid? OrderId, Guid? ProductId, int Page, int Limit)
        : IRequest<PagedResponse<OrderDetailResponseDto>>;

    public sealed class Handler : IRequestHandler<Query, PagedResponse<OrderDetailResponseDto>>
    {
        private readonly IOrderDetailRepository _details;

        public Handler(IOrderDetailRepository details) => _details = details;

        public async Task<PagedResponse<OrderDetailResponseDto>> Handle(Query request, CancellationToken ct)
        {
            var (items, total) = await _details.GetPagedDtosAsync(request.OrderId, request.ProductId, request.Page, request.Limit, ct);
            var totalPages = (int)Math.Ceiling(total / (double)request.Limit);

            return new PagedResponse<OrderDetailResponseDto>(
                items,
                new PaginationMeta(request.Page, request.Limit, total, totalPages));
        }
    }
}
