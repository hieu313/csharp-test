using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Dtos;
using OrderManagementAPI.Features;

namespace OrderManagement.Controllers;

[ApiController]
[Route("api/order-details")]
public class OrderDetailsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] OrderDetailQuery query)
    {
        var result = await _mediator
            .Send(new GetOrderDetailList.Query(query.OrderId, query.ProductId, query.Page, query.Limit));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDetailRequest request)
    {
        var created = await _mediator.Send(new CreateOrderDetail.Command(
            request.OrderId!.Value,
            request.ProductId!.Value,
            request.Quantity!.Value,
            request.Discount ?? 0,
            request.Notes));
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> CreateBulk([FromBody] CreateBulkOrderDetailRequest request)
    {
        var created = await _mediator.Send(new CreateBulkOrderDetail.Command(
            request.OrderId!.Value,
            request.Items.Select(i => new CreateBulkOrderDetail.Item(
                i.ProductId!.Value,
                i.Quantity!.Value,
                i.Discount ?? 0,
                null)).ToList()));
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderDetailRequest request)
    {
        var updated = await _mediator
            .Send(new UpdateOrderDetail.Command(id, request.Quantity, request.Discount, request.Notes));
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteOrderDetail.Command(id));
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetOrderDetailById.Query(id));
        if (result is null)
            return NotFound(new { message = $"Order detail '{id}' not found" });
        return Ok(result);
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrderId(Guid orderId)
    {
        var result = await _mediator.Send(new GetOrderDetailsByOrder.Query(orderId));
        return Ok(result);
    }

    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetByProductId(Guid productId)
    {
        var result = await _mediator.Send(new GetOrderDetailsByProduct.Query(productId));
        return Ok(result);
    }
}
