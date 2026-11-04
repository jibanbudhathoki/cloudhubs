using System.ComponentModel.DataAnnotations;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Web.ViewModels.Users;

public class UserFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = nameof(UserRole.Viewer);

    public bool IsActive { get; set; } = true;
}
