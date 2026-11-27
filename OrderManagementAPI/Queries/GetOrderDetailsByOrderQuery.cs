using MediatR;
using OrderManagement.Dtos;

namespace OrderManagementAPI.Queries;

public sealed record GetOrderDetailsByOrderQuery(Guid OrderId) : IRequest<OrderDetailsByOrderResponse>;
