using BonusTest.Domain.Services;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;

namespace BonusTest.UnitTests.Fakes;

/// <summary>Repositorio en memoria. Sin BD, todos los bonos tienen BonusId = 0.</summary>
internal sealed class FakeBonusRepository : IBonusRepository
{
    public List<Employee> Employees { get; } = [];
    public List<Bonus> Bonuses { get; } = [];
    public HashSet<int> BonusTypeIds { get; } = [BonusTypes.FixedAmount, BonusTypes.SalaryPercentage];
    public HashSet<string> ActiveReasonCodes { get; } = ["000001", "000002", "000003"];
    public int SaveChangesCalls { get; private set; }

    public Task<PagedResponse<BonusListItemDto>> GetPagedAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        throw new NotSupportedException("El listado se prueba contra la base de datos.");

    public Task<Employee?> GetEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.FirstOrDefault(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber));

    public Task<bool> BonusTypeExistsAsync(int bonusTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(BonusTypeIds.Contains(bonusTypeId));

    public Task<bool> IsReasonActiveAsync(string reasonCode, CancellationToken cancellationToken) =>
        Task.FromResult(ActiveReasonCodes.Contains(reasonCode));

    public Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, string period, CancellationToken cancellationToken) =>
        Task.FromResult(Bonuses.Any(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber
            && x.Period == period && !x.IsDeleted && x.BonusStateCode != BonusStates.Cancelled));

    public Task<Bonus?> GetByIdAsync(int bonusId, CancellationToken cancellationToken) =>
        Task.FromResult(Bonuses.LastOrDefault(x => x.BonusId == bonusId && !x.IsDeleted));

    public void Add(Bonus bonus) => Bonuses.Add(bonus);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        return Task.CompletedTask;
    }

    public List<ProjectHours> Projects { get; } = [];
    public List<BonusPaymentDetail> PaymentDetails { get; } = [];

    public Task<IReadOnlyList<ProjectHours>> GetProjectHoursAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectHours>>(Projects);

    public Task SavePaymentAsync(IReadOnlyList<BonusPaymentDetail> details, CancellationToken cancellationToken)
    {
        PaymentDetails.AddRange(details);
        SaveChangesCalls++;
        return Task.CompletedTask;
    }

    public List<BonusStateTotals> StateTotals { get; } = [];
    public List<BonusListItemDto> ExportRows { get; } = [];
    public List<PaymentLineDto> PaymentLines { get; } = [];

    public Task<IReadOnlyList<BonusStateTotals>> GetStateTotalsAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BonusStateTotals>>(StateTotals);

    public Task<IReadOnlyList<BonusListItemDto>> GetForExportAsync(BonusFilter filter, int maxRows, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BonusListItemDto>>(ExportRows.Take(maxRows).ToList());

    public Task<IReadOnlyList<PaymentLineDto>> GetPaymentLinesAsync(int bonusId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PaymentLineDto>>(PaymentLines);
}
