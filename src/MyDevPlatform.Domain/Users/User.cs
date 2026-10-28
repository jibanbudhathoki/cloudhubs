namespace MyDevPlatform.Domain.Users;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string? LoginId { get; set; }
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public bool MustChangePasswordOnNextLogin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
