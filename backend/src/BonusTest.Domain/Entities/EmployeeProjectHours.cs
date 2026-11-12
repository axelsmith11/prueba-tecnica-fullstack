namespace BonusTest.Domain.Entities;

public class EmployeeProjectHours
{
    public int EmployeeProjectHoursId { get; set; }
    public string DepartmentCode { get; set; } = null!;
    public int EmployeeNumber { get; set; }
    public string ProjectName { get; set; } = null!;
    public decimal Hours { get; set; }
}
