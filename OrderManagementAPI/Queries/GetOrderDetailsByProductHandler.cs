using MediatR;
using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Queries;

public sealed class GetOrderDetailsByProductHandler : IRequestHandler<GetOrderDetailsByProductQuery, OrderDetailsByProductResponse>
{
    private readonly IOrderDetailRepository _details;
    private readonly IProductRepository _products;

    public GetOrderDetailsByProductHandler(IOrderDetailRepository details, IProductRepository products)
    {
        _details = details;
        _products = products;
    }

    public async Task<OrderDetailsByProductResponse> Handle(GetOrderDetailsByProductQuery request, CancellationToken ct)
    {
        if (!await _products.ExistsAsync(request.ProductId))
            throw new NotFoundException($"Product '{request.ProductId}' not found");

        var data = await _details.GetDtosByProductIdAsync(request.ProductId);
        var summary = await _details.GetSalesSummaryByProductAsync(request.ProductId);

        return new OrderDetailsByProductResponse(data, summary);
    }
}
