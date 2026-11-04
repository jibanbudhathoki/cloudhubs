using System.ComponentModel.DataAnnotations;

namespace CloudHubs.Web.ViewModels.Projects;

public class PlatformProjectFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [RegularExpression("^[a-zA-Z0-9_-]+$", ErrorMessage = "Use letters, numbers, hyphens, or underscores.")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string RootPath { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string TemplateId { get; set; } = "default";

    public bool IsActive { get; set; } = true;
}
