using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Services;
using BonusTest.Domain.Exceptions;
using BonusTest.Infrastructure.Persistence;

namespace BonusTest.Infrastructure.Repositories;

public class BonusRepository(AppDbContext context) : IBonusRepository
{
    /// <summary>Violación de índice único (UX_Bonus_ActivePerPeriod) por registros concurrentes.</summary>
    private const int UniqueIndexViolation = 2601;

    public async Task<PagedResponse<BonusListItemDto>> GetPagedAsync(BonusFilter filter, CancellationToken cancellationToken)
    {
        var bonuses = VisibleBonuses(filter);

        var totalRecords = await bonuses.CountAsync(cancellationToken);
        var page = bonuses
            .OrderByDescending(x => x.BonusId)
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize);
        var data = await ToListItems(page).ToListAsync(cancellationToken);

        return PagedResponse<BonusListItemDto>.Create(data, filter.PageIndex, filter.PageSize, totalRecords);
    }

    public Task<Employee?> GetEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber, cancellationToken);

    public Task<bool> BonusTypeExistsAsync(int bonusTypeId, CancellationToken cancellationToken) =>
        context.BonusTypes.AnyAsync(x => x.BonusTypeId == bonusTypeId, cancellationToken);

    public Task<bool> IsReasonActiveAsync(string reasonCode, CancellationToken cancellationToken) =>
        context.BonusReasons.AnyAsync(x => x.ReasonCode == reasonCode && x.State, cancellationToken);

    public Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, string period, CancellationToken cancellationToken) =>
        context.Bonuses.AnyAsync(x => x.DepartmentCode == departmentCode
            && x.EmployeeNumber == employeeNumber
            && x.Period == period
            && !x.IsDeleted
            && x.BonusStateCode != BonusStates.Cancelled, cancellationToken);

    public Task<Bonus?> GetByIdAsync(int bonusId, CancellationToken cancellationToken) =>
        context.Bonuses.FirstOrDefaultAsync(x => x.BonusId == bonusId && !x.IsDeleted, cancellationToken);

    public void Add(Bonus bonus) => context.Bonuses.Add(bonus);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => SaveTranslatingErrorsAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectHours>> GetProjectHoursAsync(string departmentCode, int employeeNumber,
        CancellationToken cancellationToken) =>
        await context.EmployeeProjectHours.AsNoTracking()
            .Where(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber && x.Hours > 0)
            .OrderBy(x => x.EmployeeProjectHoursId)
            .Select(x => new ProjectHours(x.ProjectName, x.Hours))
            .ToListAsync(cancellationToken);

    public async Task SavePaymentAsync(IReadOnlyList<BonusPaymentDetail> details, CancellationToken cancellationToken)
    {
        // Transacción explícita: el detalle y el paso a Pagado se confirman juntos o no se guarda nada.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.BonusPaymentDetails.AddRange(details);
        await SaveTranslatingErrorsAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// BonusStateCode e IsDeleted son tokens de concurrencia: si otra petición ya cambió el bono, el UPDATE
    /// afecta 0 filas y se informa como "no está Registrado" (evita pagar o anular dos veces).
    /// </summary>
    private async Task SaveTranslatingErrorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw BonusErrors.BonusNotRegistered();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation })
        {
            throw BonusErrors.DuplicateBonus();
        }
    }

    /// <summary>Bonos no eliminados que cumplen el filtro, sin seguimiento (base de listado, resumen y exportación).</summary>
    private IQueryable<Bonus> VisibleBonuses(BonusFilter filter) =>
        ApplyFilter(context.Bonuses.AsNoTracking().Where(x => !x.IsDeleted), filter);

    public async Task<IReadOnlyList<BonusStateTotals>> GetStateTotalsAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        await VisibleBonuses(filter)
            .GroupBy(x => x.BonusStateCode)
            .Select(g => new BonusStateTotals(g.Key, g.Count(), g.Sum(x => x.GrossAmount), g.Sum(x => x.TaxAmount), g.Sum(x => x.NetAmount)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BonusListItemDto>> GetForExportAsync(BonusFilter filter, int maxRows,
        CancellationToken cancellationToken) =>
        await ToListItems(VisibleBonuses(filter).OrderByDescending(x => x.BonusId).Take(maxRows)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentLineDto>> GetPaymentLinesAsync(int bonusId, CancellationToken cancellationToken) =>
        await context.BonusPaymentDetails.AsNoTracking()
            .Where(x => x.BonusId == bonusId)
            .OrderBy(x => x.BonusPaymentDetailId)
            .Select(x => new PaymentLineDto(x.ProjectName, x.Hours, x.Amount))
            .ToListAsync(cancellationToken);

    private static IQueryable<Bonus> ApplyFilter(IQueryable<Bonus> bonuses, BonusFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.DepartmentCode))
            bonuses = bonuses.Where(x => x.DepartmentCode == filter.DepartmentCode);
        if (filter.EmployeeNumber is not null)
            bonuses = bonuses.Where(x => x.EmployeeNumber == filter.EmployeeNumber);
        if (!string.IsNullOrWhiteSpace(filter.Period))
            bonuses = bonuses.Where(x => x.Period == filter.Period);
        if (!string.IsNullOrWhiteSpace(filter.BonusStateCode))
            bonuses = bonuses.Where(x => x.BonusStateCode == filter.BonusStateCode);

        return bonuses;
    }

    /// <summary>Une la página con los catálogos para devolver descripciones listas para mostrar.</summary>
    private IQueryable<BonusListItemDto> ToListItems(IQueryable<Bonus> bonuses) =>
        from bonus in bonuses
        join state in context.States on bonus.BonusStateCode equals state.StateCode
        join bonusType in context.BonusTypes on bonus.BonusTypeId equals bonusType.BonusTypeId
        join reason in context.BonusReasons on bonus.ReasonCode equals reason.ReasonCode
        join employee in context.Employees
            on new { bonus.DepartmentCode, bonus.EmployeeNumber } equals new { employee.DepartmentCode, employee.EmployeeNumber }
        join department in context.Departments on bonus.DepartmentCode equals department.DepartmentCode
        orderby bonus.BonusId descending
        select new BonusListItemDto(
            bonus.BonusId,
            bonus.BonusStateCode,
            state.StateDescription,
            bonus.Period,
            bonus.DepartmentCode,
            department.DepartmentName,
            bonus.EmployeeNumber,
            employee.FullName,
            bonus.BonusTypeId,
            bonusType.BonusTypeDescription,
            bonus.BonusValue,
            reason.ReasonDescription,
            bonus.GrossAmount,
            bonus.TaxAmount,
            bonus.NetAmount);
}
