using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyDevPlatform.Api.Features.Superuser;
using MyDevPlatform.Application.Organizations;
using MyDevPlatform.Application.Projects;
using MyDevPlatform.Application.Users;
using MyDevPlatform.Infrastructure.DependencyInjection;
using MyDevPlatform.Infrastructure.Persistence;
using Serilog;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddControllersWithViews();
var jwtSigningKey = builder.Configuration["Superuser:Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("Set SUPERUSER__JWT__SIGNINGKEY to a secret of at least 32 UTF-8 bytes.");
}

var jwtIssuer = builder.Configuration["Superuser:Jwt:Issuer"] ?? "CloudHubs";
var jwtAudience = builder.Configuration["Superuser:Jwt:Audience"] ?? "CloudHubs.Api";
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MyDevPlatform.Superuser";
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
builder.Services.AddScoped<PlatformSettingsService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PlatformProjectService>();
builder.Services.AddScoped<IPasswordHasher<MyDevPlatform.Domain.Users.User>, PasswordHasher<MyDevPlatform.Domain.Users.User>>();
builder.Services.AddScoped<SuperuserCredentialValidator>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=mydevplatform;Username=postgres;Password=postgres";

builder.Services.AddDbContext<MyDevPlatformDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IPlatformSettingsRepository, PlatformSettingsRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPlatformProjectRepository, PlatformProjectRepository>();
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
app.UseAuthentication();
app.UseAuthorization();
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
