using Guardiao.Infrastructure.Persistence;
using Guardiao.Web.Components;
using Guardiao.Web.Security;
using Guardiao.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<OperationsSession>();
builder.Services.AddScoped<AuthenticationStateProvider, OperationsAuthenticationStateProvider>();
builder.Services.AddScoped<OperationsPanelService>();
builder.Services.AddDbContext<GuardiaoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
