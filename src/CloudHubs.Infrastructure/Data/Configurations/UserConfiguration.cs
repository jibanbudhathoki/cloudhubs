using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(200);
        entity.Property(x => x.LoginId).HasMaxLength(200);
        entity.Property(x => x.PasswordHash).HasMaxLength(512);
        entity.HasIndex(x => x.Email).IsUnique();
        entity.HasIndex(x => x.LoginId).IsUnique().HasFilter("\"LoginId\" IS NOT NULL");
    }
}
