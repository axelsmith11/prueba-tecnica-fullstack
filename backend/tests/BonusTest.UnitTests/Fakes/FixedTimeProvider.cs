namespace BonusTest.UnitTests.Fakes;

/// <summary>Reloj fijo en UTC para probar reglas que dependen de la fecha actual.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
