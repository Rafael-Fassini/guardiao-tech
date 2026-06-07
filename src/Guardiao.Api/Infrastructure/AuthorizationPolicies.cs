namespace Guardiao.Api.Infrastructure;

public static class AuthorizationPolicies
{
    public const string MetadataRead = "MetadataRead";
    public const string CasesRead = "CasesRead";
    public const string RulesManage = "RulesManage";
    public const string IncidentsRead = "IncidentsRead";
    public const string IncidentsReview = "IncidentsReview";
    public const string AuditRead = "AuditRead";
    public const string CandidateEventsIngest = "CandidateEventsIngest";
}
