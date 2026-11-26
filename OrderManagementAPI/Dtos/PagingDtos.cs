using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Dtos;

public class OrderDetailQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int Limit { get; set; } = 20;
    public Guid? OrderId { get; set; }
    public Guid? ProductId { get; set; }
}

public record PaginationMeta(int Page, int Limit, int Total, int TotalPages);

public record PagedResponse<T>(IReadOnlyList<T> Data, PaginationMeta Pagination);
