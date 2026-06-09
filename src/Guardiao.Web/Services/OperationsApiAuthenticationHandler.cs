using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Guardiao.Web.Services;

public sealed class OperationsApiAuthenticationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OperationsPanelOptions _options;

    public OperationsApiAuthenticationHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<OperationsPanelOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var userName = user.Identity.Name ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = user.FindFirstValue(ClaimTypes.Role) ?? "viewer";
            if (!string.IsNullOrWhiteSpace(userName))
            {
                request.Headers.Remove("X-Panel-User");
                request.Headers.Remove("X-Panel-Role");
                request.Headers.Remove("X-Panel-Auth");
                request.Headers.TryAddWithoutValidation("X-Panel-User", userName);
                request.Headers.TryAddWithoutValidation("X-Panel-Role", role);
                request.Headers.TryAddWithoutValidation("X-Panel-Auth", _options.SharedSecret);
            }
        }

        if (!request.Headers.Accept.Any(x => x.MediaType == "application/json"))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
