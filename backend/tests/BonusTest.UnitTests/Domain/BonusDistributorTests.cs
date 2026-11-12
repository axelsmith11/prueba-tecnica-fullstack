using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.UnitTests.Domain;

public class BonusDistributorTests
{
    [Fact]
    public void Distributes_proportionally_to_hours()
    {
        // TECH1-102: 45 / 35 / 20 horas, bruto 300.00
        var allocations = BonusDistributor.Distribute(300m,
            [new("Portal de clientes", 45m), new("App móvil", 35m), new("Soporte", 20m)]);

        Assert.Equal(new[] { 135m, 105m, 60m }, allocations.Select(x => x.Amount));
        Assert.Equal(new[] { "Portal de clientes", "App móvil", "Soporte" }, allocations.Select(x => x.ProjectName));
    }

    [Fact]
    public void Last_project_absorbs_rounding_difference()
    {
        var allocations = BonusDistributor.Distribute(100m,
            [new("A", 10m), new("B", 10m), new("C", 10m)]);

        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, allocations.Select(x => x.Amount));
        Assert.Equal(100m, allocations.Sum(x => x.Amount));
    }

    [Fact]
    public void Each_line_rounds_half_up()
    {
        // 0.25 × 1/2 = 0.125 → 0.13; el último recibe 0.12
        var allocations = BonusDistributor.Distribute(0.25m, [new("A", 1m), new("B", 1m)]);

        Assert.Equal(new[] { 0.13m, 0.12m }, allocations.Select(x => x.Amount));
    }

    [Fact]
    public void Single_project_receives_everything()
    {
        var allocation = Assert.Single(BonusDistributor.Distribute(250.55m, [new("Campaña de verano", 160m)]));

        Assert.Equal(250.55m, allocation.Amount);
        Assert.Equal(160m, allocation.Hours);
    }

    [Fact]
    public void Fails_when_employee_has_no_projects()
    {
        var error = Assert.Throws<BusinessException>(() => BonusDistributor.Distribute(100m, []));

        Assert.Equal("EMPLOYEE_WITHOUT_PROJECTS", error.Code);
    }
}
