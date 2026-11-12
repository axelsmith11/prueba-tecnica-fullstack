using BonusTest.Application.Common;

namespace BonusTest.Application.Bonuses;

/// <summary>Filtros opcionales del listado de bonos.</summary>
public class BonusFilter : PaginationRequest
{
    public string? DepartmentCode { get; set; }
    public int? EmployeeNumber { get; set; }
    public string? Period { get; set; }
    public string? BonusStateCode { get; set; }
}

public record BonusListItemDto(
    int BonusId,
    string BonusStateCode,
    string StateDescription,
    string Period,
    string DepartmentCode,
    string DepartmentName,
    int EmployeeNumber,
    string FullName,
    int BonusTypeId,
    string BonusTypeDescription,
    decimal BonusValue,
    string ReasonDescription,
    decimal GrossAmount,
    decimal TaxAmount,
    decimal NetAmount);

public record EmployeeLookupDto(
    string DepartmentCode,
    int EmployeeNumber,
    string FullName,
    DateTime HireDate,
    decimal MonthlySalary,
    decimal TaxRate);

public record RegisterBonusRequest(
    string DepartmentCode,
    int EmployeeNumber,
    string Period,
    int BonusTypeId,
    decimal BonusValue,
    string ReasonCode);

public record RegisteredBonusDto(int BonusId, decimal GrossAmount, decimal TaxAmount, decimal NetAmount);

public record PaymentLineDto(string ProjectName, decimal Hours, decimal Amount);

public record BonusPaymentDetailDto(int BonusId, decimal GrossAmount, IReadOnlyList<PaymentLineDto> Lines);

/// <summary>Archivo listo para descargar (bytes con BOM UTF-8 y nombre sugerido).</summary>
public record BonusExportFile(byte[] Content, string FileName);
