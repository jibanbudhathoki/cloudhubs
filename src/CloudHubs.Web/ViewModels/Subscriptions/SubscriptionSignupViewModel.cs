using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Subscriptions;

public sealed class SubscriptionSignupViewModel
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public Guid PlanId { get; set; }

    public IReadOnlyCollection<CloudHubs.Domain.Entities.SubscriptionPlan> Plans { get; set; } = [];
}
