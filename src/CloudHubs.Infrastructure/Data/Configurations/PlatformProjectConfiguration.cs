using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class PlatformProjectConfiguration : IEntityTypeConfiguration<PlatformProject>
{
    public void Configure(EntityTypeBuilder<PlatformProject> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.RootPath).HasMaxLength(500).IsRequired();
        entity.Property(x => x.TemplateId).HasMaxLength(150).IsRequired();
        entity.HasIndex(x => x.Slug).IsUnique();
    }
}
