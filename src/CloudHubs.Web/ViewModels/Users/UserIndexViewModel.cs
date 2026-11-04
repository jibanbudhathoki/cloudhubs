using CloudHubs.Domain.Entities;

namespace CloudHubs.Web.ViewModels.Users;

public class UserIndexViewModel
{
    public IReadOnlyCollection<User> Users { get; init; } = [];
    public string? Query { get; init; }
    public bool IsDataAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
}
