using Microsoft.EntityFrameworkCore;
using BonusTest.Domain.Entities;

namespace BonusTest.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<State> States => Set<State>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeProjectHours> EmployeeProjectHours => Set<EmployeeProjectHours>();
    public DbSet<BonusType> BonusTypes => Set<BonusType>();
    public DbSet<BonusReason> BonusReasons => Set<BonusReason>();

    // agrega aquí el DbSet de Bonus y su configuración.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
