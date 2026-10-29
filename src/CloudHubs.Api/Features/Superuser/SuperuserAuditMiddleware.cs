using CloudHubs.Application.Organizations;
using System.Security.Claims;

namespace CloudHubs.Api.Features.Superuser;

public class SuperuserAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        AuditLogService auditLogService,
        ILogger<SuperuserAuditMiddleware> logger)
    {
        await next(context);

        var request = context.Request;
        var path = request.Path.Value ?? "/su";
        var isWrite = request.Method is "POST" or "PUT" or "PATCH" or "DELETE";
        var isSuperuserAction = request.Path.StartsWithSegments("/su")
            && context.User.IsInRole("Superuser");
        var isTenantAction = request.Path.StartsWithSegments("/api/organizations")
            && context.User.IsInRole("OrganizationMember");
        if (!isWrite || (!isSuperuserAction && !isTenantAction))
        {
            return;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        const int organizationIndex = 2;
        Guid? organizationId = segments.Length > organizationIndex
            && Guid.TryParse(segments[organizationIndex], out var parsedOrganizationId)
                ? parsedOrganizationId
                : null;
        var actorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.Identity?.Name
            ?? "unknown";
        var actorName = context.User.FindFirstValue(ClaimTypes.Name)
            ?? context.User.Identity?.Name
            ?? actorId;
        var resourceIndex = isTenantAction ? 3 : 1;
        var resourceType = segments.Length > resourceIndex ? Limit(segments[resourceIndex], 100) : "platform";
        var resourceId = segments.Skip(resourceIndex + 1).FirstOrDefault(segment => Guid.TryParse(segment, out _)) ?? string.Empty;
        var action = Limit($"{request.Method} {path}", 100);

        try
        {
            await auditLogService.RecordAsync(
                actorId,
                actorName,
                action,
                resourceType,
                resourceId,
                organizationId,
                $"Response status: {context.Response.StatusCode}.",
                context.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to write the Superadmin audit event for {Method} {Path}.", request.Method, path);
        }
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
