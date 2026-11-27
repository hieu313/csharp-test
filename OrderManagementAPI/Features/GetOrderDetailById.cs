using MediatR;
using OrderManagement.Dtos;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public class GetOrderDetailById
{
    public sealed record Query(Guid Id) : IRequest<OrderDetailResponseDto?>;

    public sealed class Handler : IRequestHandler<Query, OrderDetailResponseDto?>
    {
        private readonly IOrderDetailRepository _details;

        public Handler(IOrderDetailRepository details) => _details = details;

        public Task<OrderDetailResponseDto?> Handle(Query request, CancellationToken ct)
            => _details.GetDtoByIdAsync(request.Id, ct);
    }
}
