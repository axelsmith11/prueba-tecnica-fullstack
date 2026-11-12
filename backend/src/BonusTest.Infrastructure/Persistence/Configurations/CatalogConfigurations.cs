using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BonusTest.Domain.Entities;

namespace BonusTest.Infrastructure.Persistence.Configurations;

public class StateConfiguration : IEntityTypeConfiguration<State>
{
    public void Configure(EntityTypeBuilder<State> builder)
    {
        builder.ToTable("State").HasKey(x => x.StateCode);
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Department").HasKey(x => x.DepartmentCode);
    }
}

public class BonusTypeConfiguration : IEntityTypeConfiguration<BonusType>
{
    public void Configure(EntityTypeBuilder<BonusType> builder)
    {
        builder.ToTable("BonusType").HasKey(x => x.BonusTypeId);
        builder.Property(x => x.BonusTypeId).ValueGeneratedNever();
    }
}

public class BonusReasonConfiguration : IEntityTypeConfiguration<BonusReason>
{
    public void Configure(EntityTypeBuilder<BonusReason> builder)
    {
        builder.ToTable("BonusReason").HasKey(x => x.ReasonCode);
    }
}
