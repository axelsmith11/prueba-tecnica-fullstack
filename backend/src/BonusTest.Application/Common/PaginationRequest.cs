namespace BonusTest.Application.Common;

/// <summary>
/// Parámetros base de paginación. Hereda de esta clase en tus filtros.
/// </summary>
public class PaginationRequest
{
    private const int MaxPageSize = 50;
    private int _pageIndex = 1;
    private int _pageSize = 10;

    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 10 : Math.Min(value, MaxPageSize);
    }
}
