using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BonusTest.Domain.Entities;

namespace BonusTest.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapea dbo.Bonus (creada por database/02_bonus.sql). Los tipos char/varchar evitan
/// conversiones implícitas a nvarchar que impedirían usar los índices.
/// </summary>
public class BonusConfiguration : IEntityTypeConfiguration<Bonus>
{
    public void Configure(EntityTypeBuilder<Bonus> builder)
    {
        builder.ToTable("Bonus").HasKey(x => x.BonusId);

        builder.Property(x => x.DepartmentCode).HasColumnType("char(5)");
        builder.Property(x => x.Period).HasColumnType("char(7)");
        builder.Property(x => x.ReasonCode).HasColumnType("char(6)");
        builder.Property(x => x.BonusStateCode).HasColumnType("char(4)");
        builder.Property(x => x.CreatedBy).HasColumnType("varchar(50)");
        builder.Property(x => x.UpdatedBy).HasColumnType("varchar(50)");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.BonusValue).HasPrecision(16, 2);
        builder.Property(x => x.GrossAmount).HasPrecision(16, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(16, 2);
        builder.Property(x => x.NetAmount).HasPrecision(16, 2);
        builder.Property(x => x.TaxRateApplied).HasPrecision(5, 2);

        builder.HasOne<Employee>().WithMany().HasForeignKey(x => new { x.DepartmentCode, x.EmployeeNumber });
        builder.HasOne<BonusType>().WithMany().HasForeignKey(x => x.BonusTypeId);
        builder.HasOne<BonusReason>().WithMany().HasForeignKey(x => x.ReasonCode);
        builder.HasOne<State>().WithMany().HasForeignKey(x => x.BonusStateCode);
    }
}
