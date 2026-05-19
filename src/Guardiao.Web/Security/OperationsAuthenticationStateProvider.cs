using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Guardiao.Web.Security;

public sealed class OperationsAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly OperationsSession _session;

    public OperationsAuthenticationStateProvider(OperationsSession session)
    {
        _session = session;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_session.IsAuthenticated)
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, _session.UserName),
            new Claim(ClaimTypes.Role, _session.Role)
        };

        var identity = new ClaimsIdentity(claims, "operations-session");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public void NotifyStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
