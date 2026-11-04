using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class AccountSubscriptionConfiguration : IEntityTypeConfiguration<AccountSubscription>
{
    public void Configure(EntityTypeBuilder<AccountSubscription> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Status).IsRequired();
        entity.Property(x => x.PlanName).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        entity.Property(x => x.Price).HasPrecision(12, 2);
        entity.Property(x => x.BillingInterval).IsRequired();
        entity.HasOne(x => x.OwnerUser)
            .WithOne(x => x.AccountSubscription)
            .HasForeignKey<AccountSubscription>(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => x.OwnerUserId).IsUnique();
    }
}
