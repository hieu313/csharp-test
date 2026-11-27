using OrderManagement.Dtos;
using OrderManagement.Exceptions;
using OrderManagement.Repositories;

namespace OrderManagementAPI.Queries;

public sealed class GetOrderDetailsByProductHandler
{
    private readonly IOrderDetailRepository _details;
    private readonly IProductRepository _products;

    public GetOrderDetailsByProductHandler(IOrderDetailRepository details, IProductRepository products)
    {
        _details = details;
        _products = products;
    }

    public async Task<OrderDetailsByProductResponse> HandleAsync(GetOrderDetailsByProductQuery query)
    {
        if (!await _products.ExistsAsync(query.ProductId))
            throw new NotFoundException($"Product '{query.ProductId}' not found");

        var data = await _details.GetDtosByProductIdAsync(query.ProductId);
        var summary = await _details.GetSalesSummaryByProductAsync(query.ProductId);

        return new OrderDetailsByProductResponse(data, summary);
    }
}
