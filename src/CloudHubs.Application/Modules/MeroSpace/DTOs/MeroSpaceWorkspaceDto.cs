namespace CloudHubs.Application.Modules.MeroSpace.DTOs;

public sealed record MeroSpaceWorkspaceDto(
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    string TemplateId,
    string TemplateName,
    string RootPath,
    DateTime CreatedAt,
    bool IsActive);

