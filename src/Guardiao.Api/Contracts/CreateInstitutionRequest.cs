using System.ComponentModel.DataAnnotations;

namespace Guardiao.Api.Contracts;

public class CreateInstitutionRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;
}
