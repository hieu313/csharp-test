using Microsoft.AspNetCore.Mvc;
using OrderManagement.Dtos;
using OrderManagement.Services;

namespace OrderManagement.Controllers;

[ApiController]
[Route("api/order-details")]
public class OrderDetailsController : ControllerBase
{
    private readonly IOrderDetailService _service;
    public OrderDetailsController(IOrderDetailService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var orderDetail = await _service.GetByIdAsync(id);
        if (orderDetail is null)
            return NotFound(new { message = $"Order detail '{id}' not found" });
        return Ok(orderDetail);
    }
}

