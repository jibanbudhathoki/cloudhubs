using Microsoft.EntityFrameworkCore;
using CloudHubs.Domain.Organizations;
using CloudHubs.Domain.Projects;
using CloudHubs.Domain.Subscriptions;
using CloudHubs.Domain.Users;

namespace CloudHubs.Infrastructure.Persistence;

public class CloudHubsDbContext : DbContext
{
    public CloudHubsDbContext(DbContextOptions<CloudHubsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PlatformProject> PlatformProjects => Set<PlatformProject>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<AccountSubscription> AccountSubscriptions => Set<AccountSubscription>();
    public DbSet<SubscriptionPurchase> SubscriptionPurchases => Set<SubscriptionPurchase>();
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
            entity.HasOne(x => x.AccountSubscription)
                .WithMany()
                .HasForeignKey(x => x.AccountSubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.PlanName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.BillingInterval).IsRequired();
            entity.HasOne(x => x.Plan)
                .WithMany()
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.OrganizationId).IsUnique();
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Price).HasPrecision(12, 2);
            entity.Property(x => x.BillingInterval).IsRequired();
            entity.Property(x => x.SupportResponseHours).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AccountSubscription>(entity =>
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
        });

        modelBuilder.Entity<SubscriptionPurchase>(entity =>
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

        modelBuilder.Entity<OrganizationMembership>(entity =>
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
        });

        modelBuilder.Entity<OrganizationRole>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Permissions).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.HasOne(x => x.Organization)
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
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
