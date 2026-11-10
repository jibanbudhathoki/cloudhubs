using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CloudHubs.Domain.Entities;

namespace CloudHubs.Infrastructure.Data.Configurations;

internal sealed class ProjectIssueConfiguration : IEntityTypeConfiguration<ProjectIssue>
{
    public void Configure(EntityTypeBuilder<ProjectIssue> entity)
    {
        entity.ToTable("ProjectIssues");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.IssueKey).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Title).HasMaxLength(250).IsRequired();
        entity.Property(x => x.Description).HasColumnType("text").IsRequired(false);
        entity.Property(x => x.EstimatedHours).HasPrecision(6, 2).IsRequired(false);
        entity.Property(x => x.SpentHours).HasPrecision(6, 2).IsRequired(false);

        entity.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Reporter)
            .WithMany()
            .HasForeignKey(x => x.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Assignee)
            .WithMany()
            .HasForeignKey(x => x.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(x => new { x.OrganizationId, x.ProjectId });
        entity.HasIndex(x => new { x.OrganizationId, x.IssueKey });
        entity.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}

internal sealed class ProjectIssueCommentConfiguration : IEntityTypeConfiguration<ProjectIssueComment>
{
    public void Configure(EntityTypeBuilder<ProjectIssueComment> entity)
    {
        entity.ToTable("ProjectIssueComments");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.CommentText).HasColumnType("text").IsRequired();

        entity.HasOne(x => x.Issue)
            .WithMany(x => x.Comments)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProjectIssueActivityConfiguration : IEntityTypeConfiguration<ProjectIssueActivity>
{
    public void Configure(EntityTypeBuilder<ProjectIssueActivity> entity)
    {
        entity.ToTable("ProjectIssueActivities");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Details).HasMaxLength(1000).IsRequired();

        entity.HasOne(x => x.Issue)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

