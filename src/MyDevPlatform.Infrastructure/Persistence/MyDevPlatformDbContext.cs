using Microsoft.EntityFrameworkCore;
using MyDevPlatform.Domain.Organizations;
using MyDevPlatform.Domain.Projects;
using MyDevPlatform.Domain.Subscriptions;
using MyDevPlatform.Domain.Users;

namespace MyDevPlatform.Infrastructure.Persistence;

public class MyDevPlatformDbContext : DbContext
{
    public MyDevPlatformDbContext(DbContextOptions<MyDevPlatformDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PlatformProject> PlatformProjects => Set<PlatformProject>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ApiKey).HasMaxLength(256);
            entity.Property(x => x.BaseApiUrl).HasMaxLength(500);
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.PlanName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.HasIndex(x => x.OrganizationId).IsUnique();
        });

        modelBuilder.Entity<PlatformSettings>(entity =>
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
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.LoginId).HasMaxLength(200);
            entity.Property(x => x.PasswordHash).HasMaxLength(512);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.LoginId).IsUnique().HasFilter("\"LoginId\" IS NOT NULL");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TemplateId).HasMaxLength(150).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
        });

        modelBuilder.Entity<PlatformProject>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.RootPath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.TemplateId).HasMaxLength(150).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
        });
    }
}
