namespace BonusTest.Application.Catalogs;

public record DepartmentDto(string DepartmentCode, string DepartmentName);

public record StateDto(string StateCode, string StateDescription);

public record BonusTypeDto(int BonusTypeId, string BonusTypeDescription);

public record BonusReasonDto(string ReasonCode, string ReasonDescription);
