using BonusTest.Domain.Constants;
using BonusTest.Domain.Exceptions;

namespace BonusTest.Domain.Services;

public record BonusAmounts(decimal Gross, decimal Tax, decimal Net);

/// <summary>Calcula bruto, retención y neto de un bono. Cada monto se redondea a 2 decimales.</summary>
public static class BonusCalculator
{
    public static BonusAmounts Calculate(int bonusTypeId, decimal bonusValue, decimal monthlySalary, decimal taxRate)
    {
        var gross = bonusTypeId switch
        {
            BonusTypes.FixedAmount => Money.Round(bonusValue),
            BonusTypes.SalaryPercentage => Money.Round(monthlySalary * bonusValue / 100m),
            _ => throw BonusErrors.InvalidBonusType()
        };
        var tax = Money.Round(gross * taxRate / 100m);

        return new BonusAmounts(gross, tax, gross - tax);
    }
}
