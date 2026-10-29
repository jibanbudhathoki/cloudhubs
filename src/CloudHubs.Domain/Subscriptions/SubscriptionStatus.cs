namespace CloudHubs.Domain.Subscriptions;

public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,
    Suspended = 3,
    Expired = 4,
    Cancelled = 5
}
