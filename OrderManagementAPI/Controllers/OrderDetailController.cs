using Microsoft.AspNetCore.Mvc;
using OrderManagement.Dtos;
using OrderManagement.Services;

namespace OrderManagement.Controllers;

[ApiController]
[Route("api/order-details")]
public class OrderDetailController(IOrderDetailService service) : ControllerBase
{
}
