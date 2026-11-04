using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Subscriptions;

public sealed class SubscriptionCompletionViewModel
{
    public Guid PurchaseId { get; set; }

    [Required]
    public string Token { get; set; } = string.Empty;

    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool ExistingAccount { get; set; }
}
