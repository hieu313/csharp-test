using Microsoft.AspNetCore.Mvc;
using OrderManagement.Dtos;
using OrderManagement.Services;
using OrderManagementAPI.Commands;

namespace OrderManagement.Controllers;

[ApiController]
[Route("api/order-details")]
public class OrderDetailsController : ControllerBase
{
    private readonly IOrderDetailService _service;
    private readonly CreateOrderDetailHandler _createHandler;
    private readonly CreateBulkOrderDetailHandler _createBulkHandler;
    private readonly UpdateOrderDetailHandler _updateHandler;
    private readonly DeleteOrderDetailHandler _deleteHandler;

    public OrderDetailsController(
        IOrderDetailService service,
        CreateOrderDetailHandler createHandler,
        CreateBulkOrderDetailHandler createBulkHandler,
        UpdateOrderDetailHandler updateHandler,
        DeleteOrderDetailHandler deleteHandler)
    {
        _service = service;
        _createHandler = createHandler;
        _createBulkHandler = createBulkHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] OrderDetailQuery query)
    {
        var result = await _service.GetListAsync(query);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDetailRequest request)
    {
        var cmd = new CreateOrderDetailCommand(
            request.OrderId!.Value,
            request.ProductId!.Value,
            request.Quantity!.Value,
            request.Discount ?? 0,
            request.Notes);

        var created = await _createHandler.HandleAsync(cmd);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> CreateBulk([FromBody] CreateBulkOrderDetailRequest request)
    {
        var cmd = new CreateBulkOrderDetailCommand(
            request.OrderId!.Value,
            request.Items.Select(i => new BulkOrderDetailItem(
                i.ProductId!.Value,
                i.Quantity!.Value,
                i.Discount ?? 0,
                null)).ToList());

        var created = await _createBulkHandler.HandleAsync(cmd);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderDetailRequest request)
    {
        var cmd = new UpdateOrderDetailCommand(id, request.Quantity, request.Discount, request.Notes);
        var updated = await _updateHandler.HandleAsync(cmd);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _deleteHandler.HandleAsync(new DeleteOrderDetailCommand(id));
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var orderDetail = await _service.GetByIdAsync(id);
        if (orderDetail is null)
            return NotFound(new { message = $"Order detail '{id}' not found" });
        return Ok(orderDetail);
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrderId(Guid orderId)
    {
        var orderDetails = await _service.GetByOrderIdAsync(orderId);
        return Ok(orderDetails);
    }

    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetByProductId(Guid productId)
    {
        var result = await _service.GetByProductIdAsync(productId);
        return Ok(result);
    }
}

