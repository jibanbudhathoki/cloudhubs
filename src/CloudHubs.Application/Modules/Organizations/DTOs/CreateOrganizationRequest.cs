namespace CloudHubs.Application.Modules.Organizations.DTOs;

public record CreateOrganizationRequest(
    string Name,
    string Email,
    string Slug,
    string OwnerName,
    string OwnerEmail);
