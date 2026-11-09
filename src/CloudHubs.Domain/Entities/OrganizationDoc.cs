using CloudHubs.Domain.Common;

namespace CloudHubs.Domain.Entities;

public class OrganizationDoc : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Category { get; set; } = "API";
    public string Description { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? TestApiKey { get; set; }
    public string? SecretKey { get; set; }
    public string? EnvironmentVariables { get; set; }
    public string? PostmanCollection { get; set; }
    public string? Content { get; set; }
    public bool IsPublicToMembers { get; set; } = true;
    public ICollection<OrganizationDocAccess> AllowedMembers { get; set; } = new List<OrganizationDocAccess>();
}

