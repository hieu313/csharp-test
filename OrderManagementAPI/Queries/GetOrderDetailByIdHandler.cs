using MediatR;
using OrderManagement.Dtos;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Queries;

public sealed class GetOrderDetailByIdHandler : IRequestHandler<GetOrderDetailByIdQuery, OrderDetailResponseDto?>
{
    private readonly IOrderDetailRepository _details;

    public GetOrderDetailByIdHandler(IOrderDetailRepository details)
        => _details = details;

    public Task<OrderDetailResponseDto?> Handle(GetOrderDetailByIdQuery request, CancellationToken ct)
        => _details.GetDtoByIdAsync(request.Id);
}
