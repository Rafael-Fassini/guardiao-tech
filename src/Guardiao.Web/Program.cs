using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Security;
using Guardiao.Web.Components;
using Guardiao.Web.Security;
using Guardiao.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "guardiao-ops-auth";
    });
builder.Services.AddAuthorization();
builder.Services.AddTransient<OperationsApiAuthenticationHandler>();
builder.Services
    .AddOptions<OperationsPanelOptions>()
    .Bind(builder.Configuration.GetSection(OperationsPanelOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<OperationsPanelOptions>, OperationsPanelOptionsValidator>();
builder.Services.AddHttpClient<OperationsPanelService>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<OperationsPanelOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .ConfigurePrimaryHttpMessageHandler(sp => sp.GetService<HttpMessageHandler>() ?? new HttpClientHandler())
    .AddHttpMessageHandler<OperationsApiAuthenticationHandler>();
builder.Services
    .AddOptions<WebSecurityOptions>()
    .Bind(builder.Configuration.GetSection(WebSecurityOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<WebSecurityOptions>, WebSecurityOptionsValidator>();
builder.Services.AddSingleton<SensitiveDataRedactor>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapPost("/operations/login", async (HttpContext context, IOptions<WebSecurityOptions> securityOptions) =>
{
    if (!securityOptions.Value.EnableOperationsDemoLogin)
    {
        return Results.Redirect("/login");
    }

    var form = await context.Request.ReadFormAsync();
    var userName = form["userName"].ToString().Trim();
    var role = form["role"].ToString().Trim().ToLowerInvariant();
    var allowedRoles = new[] { "operator", "admin", "auditor" };

    if (string.IsNullOrWhiteSpace(userName) || !allowedRoles.Contains(role, StringComparer.Ordinal))
    {
        return Results.Redirect("/login");
    }

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, userName),
        new Claim(ClaimTypes.Role, role)
    };

    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    return Results.Redirect("/");
}).AllowAnonymous();
app.MapPost("/operations/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGet("/ready", async (IOptions<OperationsPanelOptions> options, CancellationToken cancellationToken) =>
{
    using var client = new HttpClient
    {
        BaseAddress = new Uri(options.Value.BaseUrl),
        Timeout = TimeSpan.FromSeconds(5)
    };
    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations/summary");
    request.Headers.TryAddWithoutValidation("X-Panel-User", "readiness-probe");
    request.Headers.TryAddWithoutValidation("X-Panel-Role", "admin");
    request.Headers.TryAddWithoutValidation("X-Panel-Auth", options.Value.SharedSecret);

    try
    {
        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? Results.Ok(new { status = "Ready" })
            : Results.Json(new { status = "NotReady", operationsApiStatusCode = (int)response.StatusCode }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "NotReady", error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.Run();

public partial class Program;
