using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> entity)
    {
        entity.HasKey(x => new { x.OrganizationId, x.UserId });
        entity.Property(x => x.Role).IsRequired();
        entity.HasOne(x => x.Organization)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.User)
            .WithMany(x => x.OrganizationMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => x.UserId);
        entity.HasOne(x => x.CustomRole)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.CustomRoleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
