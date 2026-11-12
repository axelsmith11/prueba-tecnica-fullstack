namespace BonusTest.Domain.Services;

public static class Money
{
    /// <summary>Redondea a 2 decimales con el medio hacia arriba (0.125 → 0.13).</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
