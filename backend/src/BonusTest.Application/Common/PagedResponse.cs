namespace BonusTest.Application.Common;

/// <summary>
/// Respuesta estándar para listados paginados.
/// </summary>
public class PagedResponse<T>
{
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public int TotalRecords { get; init; }
    public IReadOnlyList<T> Data { get; init; } = [];

    public static PagedResponse<T> Create(IReadOnlyList<T> data, int pageNumber, int pageSize, int totalRecords) => new()
    {
        Data = data,
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalRecords = totalRecords,
        TotalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize)
    };
}
