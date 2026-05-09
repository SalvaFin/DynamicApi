namespace Dynamic.Fidelity.Application.DTOs.Responses;

public class PaginatedResponse<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyCollection<T> Items { get; set; } = [];
}
