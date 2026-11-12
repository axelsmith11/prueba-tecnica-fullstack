using BonusTest.Domain.Exceptions;

namespace BonusTest.Domain.Services;

public record ProjectHours(string ProjectName, decimal Hours);

public record ProjectAllocation(string ProjectName, decimal Hours, decimal Amount);

/// <summary>
/// Reparte el bruto entre los proyectos en proporción a las horas. Cada línea se redondea
/// a 2 decimales y el último proyecto recibe lo que falta para cuadrar exacto.
/// </summary>
public static class BonusDistributor
{
    public static IReadOnlyList<ProjectAllocation> Distribute(decimal grossAmount, IReadOnlyList<ProjectHours> projects)
    {
        var totalHours = projects.Sum(x => x.Hours);
        if (projects.Count == 0 || totalHours <= 0)
            throw BonusErrors.EmployeeWithoutProjects();

        var allocations = new List<ProjectAllocation>(projects.Count);
        var assigned = 0m;
        foreach (var project in projects.Take(projects.Count - 1))
        {
            var amount = Money.Round(grossAmount * project.Hours / totalHours);
            allocations.Add(new ProjectAllocation(project.ProjectName, project.Hours, amount));
            assigned += amount;
        }

        var last = projects[^1];
        allocations.Add(new ProjectAllocation(last.ProjectName, last.Hours, grossAmount - assigned));
        return allocations;
    }
}
