using MediatR;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Features;

public class GetOrderDetailsByProduct
{
    public sealed record Query(Guid ProductId) : IRequest<OrderDetailsByProductResponse>;

    public sealed class Handler : IRequestHandler<Query, OrderDetailsByProductResponse>
    {
        private readonly IOrderDetailRepository _details;
        private readonly IProductRepository _products;

        public Handler(IOrderDetailRepository details, IProductRepository products)
        {
            _details = details;
            _products = products;
        }

        public async Task<OrderDetailsByProductResponse> Handle(Query request, CancellationToken ct)
        {
            if (!await _products.ExistsAsync(request.ProductId))
                throw new NotFoundException($"Product '{request.ProductId}' not found");

            var data = await _details.GetDtosByProductIdAsync(request.ProductId);
            var summary = await _details.GetSalesSummaryByProductAsync(request.ProductId);

            return new OrderDetailsByProductResponse(data, summary);
        }
    }
}
