using BonusTest.Domain.Services;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;

namespace BonusTest.UnitTests.Domain;

public class BonusTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0);
    private static readonly DateTime Later = Now.AddHours(2);

    private static Employee Andrea() => new()
    {
        DepartmentCode = "TECH1",
        EmployeeNumber = 101,
        FullName = "Andrea Torres Vega",
        HireDate = new DateTime(2022, 3, 1),
        MonthlySalary = 5000m,
        TaxRate = 8m,
        EmployeeStateCode = EmployeeStates.Active
    };

    private static Bonus NewBonus() =>
        Bonus.Create(Andrea(), "2026-10", BonusTypes.SalaryPercentage, 10m, "000001", "candidato", Now);

    [Fact]
    public void Create_sets_amounts_registered_state_and_creation_audit()
    {
        var bonus = NewBonus();

        Assert.Equal("TECH1", bonus.DepartmentCode);
        Assert.Equal(101, bonus.EmployeeNumber);
        Assert.Equal("2026-10", bonus.Period);
        Assert.Equal(BonusTypes.SalaryPercentage, bonus.BonusTypeId);
        Assert.Equal(10m, bonus.BonusValue);
        Assert.Equal("000001", bonus.ReasonCode);
        Assert.Equal(8m, bonus.TaxRateApplied);
        Assert.Equal(500m, bonus.GrossAmount);
        Assert.Equal(40m, bonus.TaxAmount);
        Assert.Equal(460m, bonus.NetAmount);
        Assert.Equal(BonusStates.Registered, bonus.BonusStateCode);
        Assert.False(bonus.IsDeleted);
        Assert.Equal("candidato", bonus.CreatedBy);
        Assert.Equal(Now, bonus.CreatedAt);
        Assert.Null(bonus.UpdatedBy);
        Assert.Null(bonus.UpdatedAt);
    }

    [Fact]
    public void Cancel_moves_registered_bonus_to_cancelled_and_audits()
    {
        var bonus = NewBonus();

        bonus.Cancel("rrhh", Later);

        Assert.Equal(BonusStates.Cancelled, bonus.BonusStateCode);
        Assert.Equal("rrhh", bonus.UpdatedBy);
        Assert.Equal(Later, bonus.UpdatedAt);
    }

    [Fact]
    public void Delete_marks_registered_bonus_as_deleted_and_audits()
    {
        var bonus = NewBonus();

        bonus.Delete("rrhh", Later);

        Assert.True(bonus.IsDeleted);
        Assert.Equal(BonusStates.Registered, bonus.BonusStateCode);
        Assert.Equal("rrhh", bonus.UpdatedBy);
        Assert.Equal(Later, bonus.UpdatedAt);
    }

    [Fact]
    public void Cancel_fails_when_bonus_is_already_cancelled()
    {
        var bonus = NewBonus();
        bonus.Cancel("rrhh", Later);

        var error = Assert.Throws<BusinessException>(() => bonus.Cancel("rrhh", Later));

        Assert.Equal("BONUS_NOT_REGISTERED", error.Code);
    }

    [Fact]
    public void Delete_fails_when_bonus_is_cancelled()
    {
        var bonus = NewBonus();
        bonus.Cancel("rrhh", Later);

        var error = Assert.Throws<BusinessException>(() => bonus.Delete("rrhh", Later));

        Assert.Equal("BONUS_NOT_REGISTERED", error.Code);
        Assert.False(bonus.IsDeleted);
    }

    [Fact]
    public void Pay_marks_bonus_as_paid_and_returns_one_detail_per_project()
    {
        var bonus = NewBonus(); // bruto 500.00

        var details = bonus.Pay([new ProjectHours("Portal de clientes", 120m), new ProjectHours("App móvil", 40m)], "rrhh", Later);

        Assert.Equal(BonusStates.Paid, bonus.BonusStateCode);
        Assert.Equal("rrhh", bonus.UpdatedBy);
        Assert.Equal(new[] { 375m, 125m }, details.Select(x => x.Amount));
        Assert.All(details, x => Assert.Equal("rrhh", x.CreatedBy));
    }

    [Fact]
    public void Pay_fails_when_bonus_is_cancelled()
    {
        var bonus = NewBonus();
        bonus.Cancel("rrhh", Later);

        var error = Assert.Throws<BusinessException>(() => bonus.Pay([new ProjectHours("A", 1m)], "rrhh", Later));

        Assert.Equal("BONUS_NOT_REGISTERED", error.Code);
    }
}
