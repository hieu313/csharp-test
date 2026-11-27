using MediatR;

namespace OrderManagementAPI.Commands;

public sealed record DeleteOrderDetailCommand(Guid Id) : IRequest;