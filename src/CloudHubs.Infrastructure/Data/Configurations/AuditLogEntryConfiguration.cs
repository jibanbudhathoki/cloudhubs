using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.ActorId).HasMaxLength(200).IsRequired();
        entity.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
        entity.Property(x => x.ResourceType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.ResourceId).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Details).HasMaxLength(2000).IsRequired();
        entity.HasIndex(x => x.OccurredAt);
        entity.HasIndex(x => new { x.OrganizationId, x.OccurredAt });
    }
}
