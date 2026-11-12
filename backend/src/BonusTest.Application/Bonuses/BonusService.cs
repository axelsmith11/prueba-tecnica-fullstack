using System.Globalization;
using System.Text;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.Application.Bonuses;

/// <summary>
/// Casos de uso del módulo de bonos. Aplica las reglas que necesitan consultar datos;
/// el cálculo y las transiciones de estado viven en el dominio.
/// </summary>
public class BonusService(IBonusRepository repository, TimeProvider timeProvider)
{
    private const string PeriodFormat = "yyyy-MM";
    private const decimal MaxPercentage = 100m;
    public const int MaxExportRows = 10_000;

    private DateTime Now => timeProvider.GetLocalNow().DateTime;

    public Task<PagedResponse<BonusListItemDto>> SearchAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        repository.GetPagedAsync(filter, cancellationToken);

    public async Task<BonusSummaryDto> GetSummaryAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        BonusSummaryCalculator.Build(await repository.GetStateTotalsAsync(filter, cancellationToken));

    /// <summary>CSV con todos los bonos del filtro (no solo la página), en UTF-8 con BOM para que Excel muestre acentos.</summary>
    public async Task<BonusExportFile> ExportAsync(BonusFilter filter, CancellationToken cancellationToken)
    {
        var rows = await repository.GetForExportAsync(filter, MaxExportRows + 1, cancellationToken);
        if (rows.Count > MaxExportRows)
            throw BonusErrors.ExportTooLarge();

        byte[] content = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(BonusCsvWriter.Write(rows))];
        return new BonusExportFile(content, $"bonos-{Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.csv");
    }

    public async Task<BonusPaymentDetailDto> GetPaymentDetailAsync(int bonusId, CancellationToken cancellationToken)
    {
        var bonus = await FindBonusAsync(bonusId, cancellationToken);
        if (bonus.BonusStateCode != BonusStates.Paid)
            throw BonusErrors.BonusNotPaid();

        var lines = await repository.GetPaymentLinesAsync(bonus.BonusId, cancellationToken);
        return new BonusPaymentDetailDto(bonus.BonusId, bonus.GrossAmount, lines);
    }

    public async Task<EmployeeLookupDto> GetActiveEmployeeAsync(string departmentCode, int employeeNumber,
        CancellationToken cancellationToken)
    {
        var employee = await FindActiveEmployeeAsync(departmentCode, employeeNumber, cancellationToken);

        return new EmployeeLookupDto(employee.DepartmentCode, employee.EmployeeNumber, employee.FullName,
            employee.HireDate, employee.MonthlySalary, employee.TaxRate);
    }

    public async Task<RegisteredBonusDto> RegisterAsync(RegisterBonusRequest request, string? userCode,
        CancellationToken cancellationToken)
    {
        var user = RequireUser(userCode);
        EnsurePeriodIsValid(request.Period);
        var employee = await FindActiveEmployeeAsync(request.DepartmentCode, request.EmployeeNumber, cancellationToken);

        if (!await repository.BonusTypeExistsAsync(request.BonusTypeId, cancellationToken))
            throw BonusErrors.InvalidBonusType();
        if (!await repository.IsReasonActiveAsync(request.ReasonCode, cancellationToken))
            throw BonusErrors.InactiveReason();
        EnsureBonusValueIsValid(request.BonusTypeId, request.BonusValue, employee.MonthlySalary);
        if (await repository.HasActiveBonusAsync(employee.DepartmentCode, employee.EmployeeNumber, request.Period, cancellationToken))
            throw BonusErrors.DuplicateBonus();

        var bonus = Bonus.Create(employee, request.Period, request.BonusTypeId, request.BonusValue,
            request.ReasonCode, user, Now);
        repository.Add(bonus);
        await repository.SaveChangesAsync(cancellationToken);

        return new RegisteredBonusDto(bonus.BonusId, bonus.GrossAmount, bonus.TaxAmount, bonus.NetAmount);
    }

    public async Task CancelAsync(int bonusId, string? userCode, CancellationToken cancellationToken)
    {
        var user = RequireUser(userCode);
        var bonus = await FindBonusAsync(bonusId, cancellationToken);

        bonus.Cancel(user, Now);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int bonusId, string? userCode, CancellationToken cancellationToken)
    {
        var user = RequireUser(userCode);
        var bonus = await FindBonusAsync(bonusId, cancellationToken);

        bonus.Delete(user, Now);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task PayAsync(int bonusId, string? userCode, CancellationToken cancellationToken)
    {
        var user = RequireUser(userCode);
        var bonus = await FindBonusAsync(bonusId, cancellationToken);
        var projects = await repository.GetProjectHoursAsync(bonus.DepartmentCode, bonus.EmployeeNumber, cancellationToken);

        var details = bonus.Pay(projects, user, Now);
        await repository.SavePaymentAsync(details, cancellationToken);
    }

    private static string RequireUser(string? userCode) =>
        string.IsNullOrWhiteSpace(userCode) ? throw BonusErrors.UserRequired() : userCode.Trim();

    private void EnsurePeriodIsValid(string period)
    {
        var isWellFormed = period?.Length == PeriodFormat.Length
            && DateTime.TryParseExact(period, PeriodFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        if (!isWellFormed)
            throw BonusErrors.InvalidPeriod();

        var currentPeriod = Now.ToString(PeriodFormat, CultureInfo.InvariantCulture);
        if (string.CompareOrdinal(period, currentPeriod) > 0)
            throw BonusErrors.FuturePeriod();
    }

    private static void EnsureBonusValueIsValid(int bonusTypeId, decimal bonusValue, decimal monthlySalary)
    {
        var hasAtMostTwoDecimals = Money.Round(bonusValue) == bonusValue;

        if (bonusTypeId == BonusTypes.FixedAmount
            && (bonusValue <= 0 || bonusValue > monthlySalary || !hasAtMostTwoDecimals))
            throw BonusErrors.InvalidAmount();

        if (bonusTypeId == BonusTypes.SalaryPercentage
            && (bonusValue <= 0 || bonusValue > MaxPercentage || !hasAtMostTwoDecimals))
            throw BonusErrors.InvalidPercentage();
    }

    private async Task<Employee> FindActiveEmployeeAsync(string departmentCode, int employeeNumber,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetEmployeeAsync(departmentCode, employeeNumber, cancellationToken)
            ?? throw BonusErrors.EmployeeNotFound();

        return employee.EmployeeStateCode == EmployeeStates.Active ? employee : throw BonusErrors.EmployeeInactive();
    }

    private async Task<Bonus> FindBonusAsync(int bonusId, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(bonusId, cancellationToken) ?? throw BonusErrors.BonusNotFound();
}
