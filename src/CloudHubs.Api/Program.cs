using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CloudHubs.Api.Features.Superuser;
using CloudHubs.Api.Features.Organizations;
using CloudHubs.Application.Organizations;
using CloudHubs.Application.Projects;
using CloudHubs.Application.Users;
using CloudHubs.Infrastructure.DependencyInjection;
using CloudHubs.Infrastructure.Persistence;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddControllersWithViews();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("tenant-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
var jwtSigningKey = builder.Configuration["Superuser:Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    if (builder.Environment.IsDevelopment())
    {
        jwtSigningKey = "CloudHubs-Development-Superuser-Jwt-Key-1234567890";
    }
    else
    {
        throw new InvalidOperationException("Set SUPERUSER__JWT__SIGNINGKEY to a secret of at least 32 UTF-8 bytes.");
    }
}

if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("Set SUPERUSER__JWT__SIGNINGKEY to a secret of at least 32 UTF-8 bytes.");
}
builder.Configuration["Superuser:Jwt:SigningKey"] = jwtSigningKey;

var jwtIssuer = builder.Configuration["Superuser:Jwt:Issuer"] ?? "CloudHubs";
var jwtAudience = builder.Configuration["Superuser:Jwt:Audience"] ?? "CloudHubs.Api";
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "CloudHubs.Superuser";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.LoginPath = "/su/login";
        options.AccessDeniedPath = "/su/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnSigningIn = async context =>
        {
            var settingsService = context.HttpContext.RequestServices.GetRequiredService<PlatformSettingsService>();
            var settings = await settingsService.GetAsync(context.HttpContext.RequestAborted);
            context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(settings.SessionTimeoutMinutes);
        };
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddCookie(TenantCookieDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "CloudHubs.Organization";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<AccountSubscriptionService>();
builder.Services.AddScoped<OrganizationTenantService>();
builder.Services.AddScoped<TenantTokenIssuer>();
builder.Services.AddScoped<PlatformSettingsService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PlatformProjectService>();
builder.Services.AddScoped<IPasswordHasher<CloudHubs.Domain.Users.User>, PasswordHasher<CloudHubs.Domain.Users.User>>();
builder.Services.AddScoped<SuperuserCredentialValidator>();
builder.Services.AddScoped<OrganizationCreatedNotificationSender>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=mydevplatform;Username=postgres;Password=postgres";

builder.Services.AddDbContext<CloudHubsDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IAccountSubscriptionRepository, AccountSubscriptionRepository>();
builder.Services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
builder.Services.AddScoped<SubscriptionPlanService>();
builder.Services.AddScoped<IOrganizationRoleRepository, OrganizationRoleRepository>();
builder.Services.AddScoped<OrganizationRoleService>();
builder.Services.AddScoped<OrganizationPermissionService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<IPlatformSettingsRepository, PlatformSettingsRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPlatformProjectRepository, PlatformProjectRepository>();
builder.Services.AddScoped<IOrganizationProjectRepository, OrganizationProjectRepository>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin();
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var credentialValidator = scope.ServiceProvider.GetRequiredService<SuperuserCredentialValidator>();
    await credentialValidator.BootstrapIfConfiguredAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.UseCors("DefaultCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SuperuserAuditMiddleware>();
app.UseStaticFiles();
app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Superuser}/{action=Index}/{id?}");

app.Run();
