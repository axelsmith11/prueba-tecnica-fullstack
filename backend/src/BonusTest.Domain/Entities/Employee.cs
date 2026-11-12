namespace BonusTest.Domain.Entities;

public class Employee
{
    public string DepartmentCode { get; set; } = null!;
    public int EmployeeNumber { get; set; }
    public string FullName { get; set; } = null!;
    public DateTime HireDate { get; set; }
    public decimal MonthlySalary { get; set; }
    /// <summary>Porcentaje de retención que se aplica a sus bonos (0, 8 o 10).</summary>
    public decimal TaxRate { get; set; }
    public string EmployeeStateCode { get; set; } = null!;

    public List<EmployeeProjectHours> ProjectHours { get; set; } = [];
}
