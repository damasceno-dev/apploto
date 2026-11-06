using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("Settings");
        builder.HasKey(setting => setting.Id);

        builder.Property(setting => setting.Version).IsRowVersion();
        builder.Property(setting => setting.LockDate).HasColumnType("date");
        builder.Property(setting => setting.DailyTargetHours).HasPrecision(6, 2);
        builder.Property(setting => setting.LunchDeductionOver6H).HasPrecision(4, 2);
        builder.Property(setting => setting.LunchDeductionOver4H).HasPrecision(4, 2);

        builder.Property(s => s.PersonalShortageThreshold).HasPrecision(14, 2);
        builder.ToTable(t => t.HasCheckConstraint("CK_Settings_PersonalShortageThreshold", "\"PersonalShortageThreshold\" IS NULL OR \"PersonalShortageThreshold\" > 0"));
        builder.HasIndex(setting => setting.BranchId).IsUnique();
    }
}
