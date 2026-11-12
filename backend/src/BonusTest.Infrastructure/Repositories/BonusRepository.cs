using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.Infrastructure.Persistence;

namespace BonusTest.Infrastructure.Repositories;

public class BonusRepository(AppDbContext context) : IBonusRepository
{
    /// <summary>Violación de índice único (UX_Bonus_ActivePerPeriod) por registros concurrentes.</summary>
    private const int UniqueIndexViolation = 2601;

    public async Task<PagedResponse<BonusListItemDto>> GetPagedAsync(BonusFilter filter, CancellationToken cancellationToken)
    {
        var bonuses = ApplyFilter(context.Bonuses.AsNoTracking().Where(x => !x.IsDeleted), filter);

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

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation })
        {
            throw BonusErrors.DuplicateBonus();
        }
    }

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
