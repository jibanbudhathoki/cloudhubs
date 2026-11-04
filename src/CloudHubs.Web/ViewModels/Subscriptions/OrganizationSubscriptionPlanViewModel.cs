using System.ComponentModel.DataAnnotations;
using CloudHubs.Domain.Enums;

namespace CloudHubs.Web.ViewModels.Subscriptions;

public class OrganizationSubscriptionPlanViewModel
{
    public Guid OrganizationId { get; set; }

    public string OrganizationName { get; set; } = string.Empty;

    public string? CurrentPlanName { get; set; }

    public string? SubscriptionStatus { get; set; }

    [Required(ErrorMessage = "Select a subscription plan.")]
    public Guid? PlanId { get; set; }
}
