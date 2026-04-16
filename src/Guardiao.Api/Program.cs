using Guardiao.Application.Ports.Inbound;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.UseCases;
using Guardiao.Infrastructure.Persistence;
using Guardiao.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core
builder.Services.AddDbContext<GuardiaoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Hexagonal wiring: API resolves inbound use cases and infrastructure adapters.
builder.Services.AddScoped<ICreateInstitutionUseCase, CreateInstitutionUseCase>();
builder.Services.AddScoped<IInstitutionRepositoryPort, InstitutionRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

public partial class Program;
