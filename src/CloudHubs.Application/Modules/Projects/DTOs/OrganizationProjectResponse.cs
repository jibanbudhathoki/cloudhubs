namespace CloudHubs.Application.Modules.Projects.DTOs;

public sealed record OrganizationProjectResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string TemplateId,
    bool IsActive,
    DateTime CreatedAt);
