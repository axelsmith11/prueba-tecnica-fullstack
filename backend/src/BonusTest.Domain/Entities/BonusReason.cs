namespace BonusTest.Domain.Entities;

public class BonusReason
{
    public string ReasonCode { get; set; } = null!;
    public string ReasonDescription { get; set; } = null!;
    public bool State { get; set; }
}
