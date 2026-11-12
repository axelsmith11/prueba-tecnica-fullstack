using BonusTest.Application.Common;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Services;

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
    Task<IReadOnlyList<ProjectHours>> GetProjectHoursAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken);
    /// <summary>Guarda el detalle y el cambio de estado del bono en una sola transacción.</summary>
    Task SavePaymentAsync(IReadOnlyList<BonusPaymentDetail> details, CancellationToken cancellationToken);
    /// <summary>Cantidad y montos de los bonos visibles (no eliminados) que cumplen el filtro, por estado.</summary>
    Task<IReadOnlyList<BonusStateTotals>> GetStateTotalsAsync(BonusFilter filter, CancellationToken cancellationToken);
    /// <summary>Bonos visibles que cumplen el filtro, más recientes primero, hasta <paramref name="maxRows"/>.</summary>
    Task<IReadOnlyList<BonusListItemDto>> GetForExportAsync(BonusFilter filter, int maxRows, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentLineDto>> GetPaymentLinesAsync(int bonusId, CancellationToken cancellationToken);
}
