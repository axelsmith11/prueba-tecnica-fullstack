using BonusTest.Domain.Constants;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.Domain.Entities;

/// <summary>
/// Bono de desempeño. Los montos se calculan al crearlo y solo cambia de estado
/// mediante sus métodos, que exigen que el bono esté en Registrado.
/// </summary>
public class Bonus
{
    public int BonusId { get; private set; }
    public string DepartmentCode { get; private set; } = null!;
    public int EmployeeNumber { get; private set; }
    public string Period { get; private set; } = null!;
    public int BonusTypeId { get; private set; }
    public decimal BonusValue { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public decimal TaxRateApplied { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string BonusStateCode { get; private set; } = null!;
    public bool IsDeleted { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Bonus() { }

    public static Bonus Create(Employee employee, string period, int bonusTypeId, decimal bonusValue,
        string reasonCode, string userCode, DateTime now)
    {
        var amounts = BonusCalculator.Calculate(bonusTypeId, bonusValue, employee.MonthlySalary, employee.TaxRate);

        return new Bonus
        {
            DepartmentCode = employee.DepartmentCode,
            EmployeeNumber = employee.EmployeeNumber,
            Period = period,
            BonusTypeId = bonusTypeId,
            BonusValue = bonusValue,
            ReasonCode = reasonCode,
            TaxRateApplied = employee.TaxRate,
            GrossAmount = amounts.Gross,
            TaxAmount = amounts.Tax,
            NetAmount = amounts.Net,
            BonusStateCode = BonusStates.Registered,
            CreatedBy = userCode,
            CreatedAt = now
        };
    }

    public void Cancel(string userCode, DateTime now)
    {
        EnsureRegistered();
        BonusStateCode = BonusStates.Cancelled;
        RegisterUpdate(userCode, now);
    }

    public void Delete(string userCode, DateTime now)
    {
        EnsureRegistered();
        IsDeleted = true;
        RegisterUpdate(userCode, now);
    }

    /// <summary>Paga el bono: reparte el bruto por proyecto y pasa a Pagado.</summary>
    public IReadOnlyList<BonusPaymentDetail> Pay(IReadOnlyList<ProjectHours> projects, string userCode, DateTime now)
    {
        EnsureRegistered();
        var details = BonusDistributor.Distribute(GrossAmount, projects)
            .Select(allocation => BonusPaymentDetail.Create(BonusId, allocation, userCode, now))
            .ToList();

        BonusStateCode = BonusStates.Paid;
        RegisterUpdate(userCode, now);
        return details;
    }

    private void EnsureRegistered()
    {
        if (IsDeleted || BonusStateCode != BonusStates.Registered)
            throw BonusErrors.BonusNotRegistered();
    }

    private void RegisterUpdate(string userCode, DateTime now)
    {
        UpdatedBy = userCode;
        UpdatedAt = now;
    }
}
