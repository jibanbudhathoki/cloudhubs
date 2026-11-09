namespace CloudHubs.Domain.Entities;

public class OrganizationDocAccess
{
    public Guid DocId { get; set; }
    public OrganizationDoc Doc { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}

