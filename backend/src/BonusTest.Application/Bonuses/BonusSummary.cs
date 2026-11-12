using BonusTest.Domain.Constants;

namespace BonusTest.Application.Bonuses;

/// <summary>Totales de los bonos visibles agrupados por estado (una fila por estado presente).</summary>
public record BonusStateTotals(string BonusStateCode, int Count, decimal GrossAmount, decimal TaxAmount, decimal NetAmount);

/// <summary>
/// Resumen para RR. HH. y Finanzas: lo pendiente de pagar, lo pagado y lo comprometido
/// (Registrado + Pagado). Los anulados solo se cuentan; no suman dinero.
/// </summary>
public record BonusSummaryDto(
    int PendingCount,
    decimal PendingNet,
    int PaidCount,
    decimal PaidNet,
    int CancelledCount,
    decimal CommittedGross,
    decimal CommittedTax,
    decimal CommittedNet);

public static class BonusSummaryCalculator
{
    public static BonusSummaryDto Build(IReadOnlyList<BonusStateTotals> totals)
    {
        var pending = TotalsFor(totals, BonusStates.Registered);
        var paid = TotalsFor(totals, BonusStates.Paid);
        var cancelled = TotalsFor(totals, BonusStates.Cancelled);

        return new BonusSummaryDto(
            pending.Count, pending.NetAmount,
            paid.Count, paid.NetAmount,
            cancelled.Count,
            pending.GrossAmount + paid.GrossAmount,
            pending.TaxAmount + paid.TaxAmount,
            pending.NetAmount + paid.NetAmount);
    }

    private static BonusStateTotals TotalsFor(IReadOnlyList<BonusStateTotals> totals, string bonusStateCode) =>
        totals.FirstOrDefault(x => x.BonusStateCode == bonusStateCode)
        ?? new BonusStateTotals(bonusStateCode, 0, 0m, 0m, 0m);
}
