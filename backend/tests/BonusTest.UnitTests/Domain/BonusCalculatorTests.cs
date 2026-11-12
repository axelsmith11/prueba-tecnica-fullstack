using BonusTest.Domain.Constants;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.UnitTests.Domain;

public class BonusCalculatorTests
{
    [Fact]
    public void Percentage_bonus_matches_statement_example()
    {
        // TECH1-101: sueldo 5,000.00, retención 8 %, bono 10 % → 500.00 / 40.00 / 460.00
        var amounts = BonusCalculator.Calculate(BonusTypes.SalaryPercentage, 10m, 5000m, 8m);

        Assert.Equal(new BonusAmounts(500.00m, 40.00m, 460.00m), amounts);
    }

    [Fact]
    public void Fixed_amount_bonus_uses_the_amount_as_gross()
    {
        var amounts = BonusCalculator.Calculate(BonusTypes.FixedAmount, 1200m, 3000m, 8m);

        Assert.Equal(new BonusAmounts(1200m, 96m, 1104m), amounts);
    }

    [Fact]
    public void Zero_tax_rate_keeps_net_equal_to_gross()
    {
        var amounts = BonusCalculator.Calculate(BonusTypes.FixedAmount, 800m, 2500m, 0m);

        Assert.Equal(new BonusAmounts(800m, 0m, 800m), amounts);
    }

    [Fact]
    public void Gross_rounds_half_up()
    {
        // 1,000.25 × 50 % = 500.125 → 500.13
        var amounts = BonusCalculator.Calculate(BonusTypes.SalaryPercentage, 50m, 1000.25m, 0m);

        Assert.Equal(500.13m, amounts.Gross);
    }

    [Fact]
    public void Tax_rounds_half_up()
    {
        // 100.25 × 10 % = 10.025 → 10.03 (el redondeo bancario daría 10.02)
        var amounts = BonusCalculator.Calculate(BonusTypes.FixedAmount, 100.25m, 3000m, 10m);

        Assert.Equal(new BonusAmounts(100.25m, 10.03m, 90.22m), amounts);
    }

    [Fact]
    public void Unknown_bonus_type_is_rejected()
    {
        var error = Assert.Throws<BusinessException>(() => BonusCalculator.Calculate(3, 10m, 5000m, 8m));

        Assert.Equal("INVALID_BONUS_TYPE", error.Code);
    }

    [Theory]
    [InlineData(0.125, 0.13)]
    [InlineData(0.124, 0.12)]
    [InlineData(2.675, 2.68)]
    [InlineData(-0.125, -0.13)]
    public void Money_round_uses_half_away_from_zero(double value, double expected)
    {
        Assert.Equal((decimal)expected, Money.Round((decimal)value));
    }
}
