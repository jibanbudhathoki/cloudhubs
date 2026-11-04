using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
        entity.Property(x => x.ApiKey).HasMaxLength(256);
        entity.Property(x => x.BaseApiUrl).HasMaxLength(500);
        entity.HasIndex(x => x.Slug).IsUnique();
        entity.HasOne(x => x.AccountSubscription)
            .WithMany()
            .HasForeignKey(x => x.AccountSubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
