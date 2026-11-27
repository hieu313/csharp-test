using MediatR;
using OrderManagement.Dtos;

namespace OrderManagementAPI.Queries;

public sealed record GetOrderDetailsByProductQuery(Guid ProductId) : IRequest<OrderDetailsByProductResponse>;