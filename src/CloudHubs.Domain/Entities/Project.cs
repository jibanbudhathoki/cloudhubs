using CloudHubs.Domain.Common;

namespace CloudHubs.Domain.Entities;

public class Project : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
