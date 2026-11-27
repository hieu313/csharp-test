using OrderManagement.Dtos;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Queries;

public sealed class GetOrderDetailByIdHandler
{
    private readonly IOrderDetailRepository _details;

    public GetOrderDetailByIdHandler(IOrderDetailRepository details)
        => _details = details;

    public Task<OrderDetailResponseDto?> HandleAsync(GetOrderDetailByIdQuery query)
        => _details.GetDtoByIdAsync(query.Id);
}
