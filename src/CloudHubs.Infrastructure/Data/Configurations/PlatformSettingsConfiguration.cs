using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.PlatformName).HasMaxLength(100).IsRequired();
        entity.Property(x => x.SupportEmail).HasMaxLength(200).IsRequired();
        entity.Property(x => x.PublicUrl).HasMaxLength(500).IsRequired();
        entity.Property(x => x.DefaultTemplateId).HasMaxLength(150).IsRequired();
        entity.Property(x => x.DefaultPlanName).HasMaxLength(100).IsRequired();
        entity.Property(x => x.BillingCurrency).HasMaxLength(3).IsRequired();
        entity.Property(x => x.NotificationEmail).HasMaxLength(200).IsRequired();
        entity.Property(x => x.MonthlyPrice).HasPrecision(12, 2);
    }
}
