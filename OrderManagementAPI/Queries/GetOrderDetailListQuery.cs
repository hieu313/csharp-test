using MediatR;
using OrderManagement.Dtos;

namespace OrderManagementAPI.Queries;

public sealed record GetOrderDetailListQuery(Guid? OrderId, Guid? ProductId, int Page, int Limit)
    : IRequest<PagedResponse<OrderDetailResponseDto>>;
