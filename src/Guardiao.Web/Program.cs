using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Persistence;
using Guardiao.Infrastructure.Security;
using Guardiao.Web.Components;
using Guardiao.Web.Security;
using Guardiao.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddScoped<OperationsSession>();
builder.Services.AddScoped<AuthenticationStateProvider, OperationsAuthenticationStateProvider>();
builder.Services.AddScoped<OperationsPanelService>();
builder.Services.AddDbContext<GuardiaoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
