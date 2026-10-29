using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CloudHubs.Application.Organizations;

namespace CloudHubs.Api.Features.Superuser;

[Route("su/audit")]
[Authorize(Roles = "Superuser")]
[AutoValidateAntiforgeryToken]
public class SuperuserAuditController(
    AuditLogService auditLogService,
    OrganizationService organizationService,
    ILogger<SuperuserAuditController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? query,
        [FromQuery] Guid? organizationId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["Section"] = "Audit";
        ViewData["Title"] = "Audit log";
        page = Math.Clamp(page, 1, 10000);
        IReadOnlyCollection<CloudHubs.Contracts.Organizations.OrganizationSummaryDto> organizations = [];

        try
        {
            organizations = await organizationService.GetOrganizationsAsync(cancellationToken);
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            {
                return View("~/Views/superuser/Pages/AuditLog.cshtml", new AuditLogIndexViewModel
                {
                    Organizations = organizations,
                    Query = query,
                    OrganizationId = organizationId,
                    From = from,
                    To = to,
                    Page = page,
                    ErrorMessage = "The start date must be on or before the end date."
                });
            }
            if (query?.Trim().Length > 200)
            {
                return View("~/Views/superuser/Pages/AuditLog.cshtml", new AuditLogIndexViewModel
                {
                    Organizations = organizations,
                    Query = query,
                    OrganizationId = organizationId,
                    From = from,
                    To = to,
                    Page = page,
                    ErrorMessage = "Audit search must be 200 characters or fewer."
                });
            }

            return View("~/Views/superuser/Pages/AuditLog.cshtml", new AuditLogIndexViewModel
            {
                Entries = await auditLogService.SearchAsync(query, organizationId, from, to, page, cancellationToken),
                Organizations = organizations,
                Query = query?.Trim(),
                OrganizationId = organizationId,
                From = from,
                To = to,
                Page = page,
                TotalCount = await auditLogService.CountAsync(query, organizationId, from, to, cancellationToken)
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to search the Superadmin audit log.");
            return View("~/Views/superuser/Pages/AuditLog.cshtml", new AuditLogIndexViewModel
            {
                Organizations = organizations,
                Query = query,
                OrganizationId = organizationId,
                From = from,
                To = to,
                Page = page,
                ErrorMessage = "Audit events are unavailable. Check the database connection and try again."
            });
        }
    }
}
