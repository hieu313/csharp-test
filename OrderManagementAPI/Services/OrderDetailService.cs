using OrderManagement.Dtos;

namespace OrderManagement.Services;

public interface IOrderDetailService
{
}

public class OrderDetailService(AppDbContext db) : IOrderDetailService
{

}
