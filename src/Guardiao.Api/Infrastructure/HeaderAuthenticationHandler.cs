using System.Security.Claims;
using System.Text.Encodings.Web;
using Guardiao.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Guardiao.Api.Infrastructure;

public sealed class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "HeaderAuth";
    private readonly ApiSecurityOptions _securityOptions;

    public HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        IOptions<ApiSecurityOptions> securityOptions,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _securityOptions = securityOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var workerId = Request.Headers["X-Worker-Id"].FirstOrDefault();
        var workerSecret = Request.Headers["X-Worker-Auth"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(_securityOptions.WorkerSharedSecret) &&
            !string.IsNullOrWhiteSpace(workerId) &&
            string.Equals(workerSecret, _securityOptions.WorkerSharedSecret, StringComparison.Ordinal))
        {
            return Task.FromResult(Success(workerId, "worker"));
        }

        var panelSubject = Request.Headers["X-Panel-User"].FirstOrDefault();
        var panelSecret = Request.Headers["X-Panel-Auth"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(_securityOptions.PanelSharedSecret) &&
            !string.IsNullOrWhiteSpace(panelSubject) &&
            string.Equals(panelSecret, _securityOptions.PanelSharedSecret, StringComparison.Ordinal))
        {
            var panelRole = Request.Headers["X-Panel-Role"].FirstOrDefault() ?? "viewer";
            return Task.FromResult(Success(panelSubject, panelRole));
        }

        if (!_securityOptions.EnableDebugHeaderAuthentication)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var subject = Request.Headers["X-Debug-User"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = Request.Headers["X-Debug-Role"].FirstOrDefault() ?? "viewer";
        return Task.FromResult(Success(subject, role));
    }

    private static AuthenticateResult Success(string subject, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, subject),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
