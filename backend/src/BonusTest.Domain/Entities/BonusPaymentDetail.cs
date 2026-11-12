using BonusTest.Domain.Services;

namespace BonusTest.Domain.Entities;

/// <summary>Parte del bono pagada a un proyecto.</summary>
public class BonusPaymentDetail
{
    public int BonusPaymentDetailId { get; private set; }
    public int BonusId { get; private set; }
    public string ProjectName { get; private set; } = null!;
    public decimal Hours { get; private set; }
    public decimal Amount { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private BonusPaymentDetail() { }

    public static BonusPaymentDetail Create(int bonusId, ProjectAllocation allocation, string userCode, DateTime now) => new()
    {
        BonusId = bonusId,
        ProjectName = allocation.ProjectName,
        Hours = allocation.Hours,
        Amount = allocation.Amount,
        CreatedBy = userCode,
        CreatedAt = now
    };
}
