using BonusTest.Application.Bonuses;
using BonusTest.Domain.Constants;

namespace BonusTest.UnitTests.Application;

public class BonusSummaryCalculatorTests
{
    [Fact]
    public void Separates_pending_paid_and_cancelled_and_commits_only_active_money()
    {
        var summary = BonusSummaryCalculator.Build(
        [
            new BonusStateTotals(BonusStates.Registered, 3, 1500m, 120m, 1380m),
            new BonusStateTotals(BonusStates.Paid, 2, 600m, 54m, 546m),
            new BonusStateTotals(BonusStates.Cancelled, 4, 900m, 72m, 828m),
        ]);

        Assert.Equal(new BonusSummaryDto(3, 1380m, 2, 546m, 4, 2100m, 174m, 1926m), summary);
    }

    [Fact]
    public void Missing_states_count_as_zero()
    {
        var summary = BonusSummaryCalculator.Build([new BonusStateTotals(BonusStates.Paid, 1, 300m, 24m, 276m)]);

        Assert.Equal(new BonusSummaryDto(0, 0m, 1, 276m, 0, 300m, 24m, 276m), summary);
    }

    [Fact]
    public void No_bonuses_gives_an_empty_summary()
    {
        Assert.Equal(new BonusSummaryDto(0, 0m, 0, 0m, 0, 0m, 0m, 0m), BonusSummaryCalculator.Build([]));
    }
}
