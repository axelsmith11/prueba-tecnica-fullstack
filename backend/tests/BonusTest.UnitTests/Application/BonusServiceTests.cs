using BonusTest.Domain.Services;
using BonusTest.Application.Bonuses;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.UnitTests.Fakes;

namespace BonusTest.UnitTests.Application;

public class BonusServiceTests
{
    private static readonly DateTimeOffset Today = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);
    private const string User = "candidato";

    private readonly FakeBonusRepository _repository = new();
    private readonly BonusService _service;

    public BonusServiceTests()
    {
        _repository.Employees.Add(new Employee
        {
            DepartmentCode = "TECH1", EmployeeNumber = 101, FullName = "Andrea Torres Vega",
            HireDate = new DateTime(2022, 3, 1), MonthlySalary = 5000m, TaxRate = 8m,
            EmployeeStateCode = EmployeeStates.Active
        });
        _repository.Employees.Add(new Employee
        {
            DepartmentCode = "VENTA", EmployeeNumber = 202, FullName = "Rosa Medina Quispe",
            HireDate = new DateTime(2020, 5, 20), MonthlySalary = 2000m, TaxRate = 8m,
            EmployeeStateCode = EmployeeStates.Terminated
        });
        _service = new BonusService(_repository, new FixedTimeProvider(Today));
    }

    private static RegisterBonusRequest Request(string period = "2026-11", int bonusTypeId = BonusTypes.SalaryPercentage,
        decimal bonusValue = 10m, string reasonCode = "000001", string departmentCode = "TECH1", int employeeNumber = 101) =>
        new(departmentCode, employeeNumber, period, bonusTypeId, bonusValue, reasonCode);

    private async Task<string> RegisterErrorCode(RegisterBonusRequest request, string? user = User)
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.RegisterAsync(request, user, CancellationToken.None));
        return error.Code;
    }

    [Fact]
    public async Task Register_saves_registered_bonus_with_calculated_amounts()
    {
        var result = await _service.RegisterAsync(Request(), User, CancellationToken.None);

        var bonus = Assert.Single(_repository.Bonuses);
        Assert.Equal(BonusStates.Registered, bonus.BonusStateCode);
        Assert.Equal(User, bonus.CreatedBy);
        Assert.Equal(Today.DateTime, bonus.CreatedAt);
        Assert.Equal(new RegisteredBonusDto(0, 500m, 40m, 460m), result);
        Assert.Equal(1, _repository.SaveChangesCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_requires_user(string? user) =>
        Assert.Equal("USER_REQUIRED", await RegisterErrorCode(Request(), user));

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026/10")]
    [InlineData("26-10")]
    [InlineData("2026-1")]
    [InlineData("")]
    public async Task Register_rejects_malformed_period(string period) =>
        Assert.Equal("INVALID_PERIOD", await RegisterErrorCode(Request(period: period)));

    [Fact]
    public async Task Register_rejects_period_after_current_month() =>
        Assert.Equal("FUTURE_PERIOD", await RegisterErrorCode(Request(period: "2026-12")));

    [Fact]
    public async Task Register_accepts_past_period()
    {
        await _service.RegisterAsync(Request(period: "2025-01"), User, CancellationToken.None);

        Assert.Single(_repository.Bonuses);
    }

    [Fact]
    public async Task Register_rejects_unknown_employee() =>
        Assert.Equal("EMPLOYEE_NOT_FOUND", await RegisterErrorCode(Request(employeeNumber: 999)));

    [Fact]
    public async Task Register_rejects_terminated_employee() =>
        Assert.Equal("EMPLOYEE_INACTIVE", await RegisterErrorCode(Request(departmentCode: "VENTA", employeeNumber: 202)));

    [Fact]
    public async Task Register_rejects_unknown_bonus_type() =>
        Assert.Equal("INVALID_BONUS_TYPE", await RegisterErrorCode(Request(bonusTypeId: 3)));

    [Fact]
    public async Task Register_rejects_inactive_reason() =>
        Assert.Equal("INACTIVE_REASON", await RegisterErrorCode(Request(reasonCode: "000004")));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5000.01)]
    [InlineData(10.555)]
    public async Task Register_rejects_invalid_fixed_amount(double amount) =>
        Assert.Equal("INVALID_AMOUNT",
            await RegisterErrorCode(Request(bonusTypeId: BonusTypes.FixedAmount, bonusValue: (decimal)amount)));

    [Fact]
    public async Task Register_accepts_fixed_amount_equal_to_salary()
    {
        var result = await _service.RegisterAsync(
            Request(bonusTypeId: BonusTypes.FixedAmount, bonusValue: 5000m), User, CancellationToken.None);

        Assert.Equal(5000m, result.GrossAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100.01)]
    [InlineData(10.555)]
    public async Task Register_rejects_invalid_percentage(double percentage) =>
        Assert.Equal("INVALID_PERCENTAGE",
            await RegisterErrorCode(Request(bonusTypeId: BonusTypes.SalaryPercentage, bonusValue: (decimal)percentage)));

    [Fact]
    public async Task Register_rejects_second_active_bonus_in_same_period()
    {
        await _service.RegisterAsync(Request(period: "2026-10"), User, CancellationToken.None);

        Assert.Equal("DUPLICATE_BONUS", await RegisterErrorCode(Request(period: "2026-10")));
    }

    [Fact]
    public async Task Register_allows_new_bonus_after_cancelling_the_previous_one()
    {
        await _service.RegisterAsync(Request(period: "2026-10"), User, CancellationToken.None);
        await _service.CancelAsync(0, User, CancellationToken.None);

        await _service.RegisterAsync(Request(period: "2026-10"), User, CancellationToken.None);

        Assert.Equal(2, _repository.Bonuses.Count);
    }

    [Fact]
    public async Task Cancel_updates_state_and_audit()
    {
        await _service.RegisterAsync(Request(), User, CancellationToken.None);

        await _service.CancelAsync(0, "rrhh", CancellationToken.None);

        var bonus = Assert.Single(_repository.Bonuses);
        Assert.Equal(BonusStates.Cancelled, bonus.BonusStateCode);
        Assert.Equal("rrhh", bonus.UpdatedBy);
        Assert.Equal(2, _repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Register_trims_user_code()
    {
        await _service.RegisterAsync(Request(), "  rrhh  ", CancellationToken.None);

        Assert.Equal("rrhh", Assert.Single(_repository.Bonuses).CreatedBy);
    }

    [Fact]
    public async Task Delete_marks_bonus_as_deleted_and_audits()
    {
        await _service.RegisterAsync(Request(), User, CancellationToken.None);

        await _service.DeleteAsync(0, "rrhh", CancellationToken.None);

        var bonus = Assert.Single(_repository.Bonuses);
        Assert.True(bonus.IsDeleted);
        Assert.Equal("rrhh", bonus.UpdatedBy);
        Assert.Equal(Today.DateTime, bonus.UpdatedAt);
    }

    [Fact]
    public async Task Cancel_fails_for_unknown_bonus()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.CancelAsync(99, User, CancellationToken.None));

        Assert.Equal("BONUS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Deleted_bonus_can_no_longer_be_found()
    {
        await _service.RegisterAsync(Request(), User, CancellationToken.None);
        await _service.DeleteAsync(0, User, CancellationToken.None);

        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.CancelAsync(0, User, CancellationToken.None));

        Assert.True(Assert.Single(_repository.Bonuses).IsDeleted);
        Assert.Equal("BONUS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Delete_requires_user()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.DeleteAsync(0, null, CancellationToken.None));

        Assert.Equal("USER_REQUIRED", error.Code);
    }

    [Fact]
    public async Task GetActiveEmployee_returns_lookup_data()
    {
        var employee = await _service.GetActiveEmployeeAsync("TECH1", 101, CancellationToken.None);

        Assert.Equal(new EmployeeLookupDto("TECH1", 101, "Andrea Torres Vega", new DateTime(2022, 3, 1), 5000m, 8m), employee);
    }

    [Fact]
    public async Task GetActiveEmployee_rejects_terminated_employee()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(
            () => _service.GetActiveEmployeeAsync("VENTA", 202, CancellationToken.None));

        Assert.Equal("EMPLOYEE_INACTIVE", error.Code);
    }

    [Fact]
    public async Task Pay_distributes_gross_by_project_and_marks_bonus_as_paid()
    {
        _repository.Projects.AddRange([new ProjectHours("Portal de clientes", 120m), new ProjectHours("App móvil", 40m)]);
        await _service.RegisterAsync(Request(), User, CancellationToken.None); // bruto 500.00

        await _service.PayAsync(0, User, CancellationToken.None);

        Assert.Equal(BonusStates.Paid, Assert.Single(_repository.Bonuses).BonusStateCode);
        Assert.Equal(new[] { 375m, 125m }, _repository.PaymentDetails.Select(x => x.Amount));
    }

    [Fact]
    public async Task Pay_fails_for_cancelled_bonus()
    {
        _repository.Projects.Add(new ProjectHours("Portal de clientes", 120m));
        await _service.RegisterAsync(Request(), User, CancellationToken.None);
        await _service.CancelAsync(0, User, CancellationToken.None);

        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.PayAsync(0, User, CancellationToken.None));

        Assert.Equal("BONUS_NOT_REGISTERED", error.Code);
        Assert.Empty(_repository.PaymentDetails);
    }
}
