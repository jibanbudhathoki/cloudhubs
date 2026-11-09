using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class OrganizationDocConfiguration : IEntityTypeConfiguration<OrganizationDoc>
{
    public void Configure(EntityTypeBuilder<OrganizationDoc> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(2000).IsRequired(false);
        entity.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired(false);
        entity.Property(x => x.ApiKey).HasMaxLength(500).IsRequired(false);
        entity.Property(x => x.TestApiKey).HasMaxLength(500).IsRequired(false);
        entity.Property(x => x.SecretKey).HasMaxLength(500).IsRequired(false);
        entity.Property(x => x.EnvironmentVariables).HasColumnType("text").IsRequired(false);
        entity.Property(x => x.PostmanCollection).HasColumnType("text").IsRequired(false);
        entity.Property(x => x.Content).HasColumnType("text").IsRequired(false);
        entity.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
    }
}

internal sealed class OrganizationDocAccessConfiguration : IEntityTypeConfiguration<OrganizationDocAccess>
{
    public void Configure(EntityTypeBuilder<OrganizationDocAccess> entity)
    {
        entity.ToTable("OrganizationDocAccess");
        entity.HasKey(x => new { x.DocId, x.UserId });
        entity.HasOne(x => x.Doc)
            .WithMany(x => x.AllowedMembers)
            .HasForeignKey(x => x.DocId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

