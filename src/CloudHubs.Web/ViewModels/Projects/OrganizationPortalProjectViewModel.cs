namespace CloudHubs.Web.ViewModels.Projects;

public sealed record OrganizationPortalProjectViewModel(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    DateTime CreatedAt,
    string TemplateId = "default",
    string TemplateName = "Default Starter",
    string RootPath = "");
