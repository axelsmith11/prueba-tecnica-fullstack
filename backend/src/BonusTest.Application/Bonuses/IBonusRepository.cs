using BonusTest.Application.Common;
using BonusTest.Domain.Entities;

namespace BonusTest.Application.Bonuses;

public interface IBonusRepository
{
    Task<PagedResponse<BonusListItemDto>> GetPagedAsync(BonusFilter filter, CancellationToken cancellationToken);
    Task<Employee?> GetEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken);
    Task<bool> BonusTypeExistsAsync(int bonusTypeId, CancellationToken cancellationToken);
    Task<bool> IsReasonActiveAsync(string reasonCode, CancellationToken cancellationToken);
    /// <summary>Activo = no eliminado y en un estado distinto de Anulado.</summary>
    Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, string period, CancellationToken cancellationToken);
    /// <summary>Devuelve el bono no eliminado, con seguimiento de cambios.</summary>
    Task<Bonus?> GetByIdAsync(int bonusId, CancellationToken cancellationToken);
    void Add(Bonus bonus);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
