using MediatR;
using OrderManagement.Dtos;

namespace OrderManagementAPI.Queries;

public sealed record GetOrderDetailByIdQuery(Guid Id) : IRequest<OrderDetailResponseDto?>;
