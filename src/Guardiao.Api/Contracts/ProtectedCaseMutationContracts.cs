using System.ComponentModel.DataAnnotations;

namespace Guardiao.Api.Contracts;

public sealed class UpdateProtectedCaseSubjectRoleRequest
{
    [Required]
    public string SubjectRole { get; set; } = string.Empty;
}
