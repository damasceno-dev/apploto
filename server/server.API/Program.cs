using server.Configuration;
using Scalar.AspNetCore;
using server;
using server.Application;
using server.Filters;
using server.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRouting(o => o.LowercaseUrls = true);
builder.Services.AddMvc(f => f.Filters.Add(typeof(ExceptionFilter)));

builder.Services.AddApi();
builder.Services.AddSecurityRateLimits();
builder.Services.AddTrustedProxies(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddAccountSecurityConfiguration(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
var app = builder.Build();

// First, so every later reader of the client address (rate limits, login budgets) sees the trusted one.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
