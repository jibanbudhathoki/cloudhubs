using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class SubscriptionPurchaseConfiguration : IEntityTypeConfiguration<SubscriptionPurchase>
{
    public void Configure(EntityTypeBuilder<SubscriptionPurchase> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
        entity.Property(x => x.PlanName).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        entity.Property(x => x.Price).HasPrecision(12, 2);
        entity.Property(x => x.BillingInterval).IsRequired();
        entity.Property(x => x.Status).IsRequired();
        entity.Property(x => x.CompletionTokenHash).HasMaxLength(64);
        entity.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => x.Email)
            .IsUnique()
            .HasFilter("\"Status\" IN (0, 1)");
    }
}
