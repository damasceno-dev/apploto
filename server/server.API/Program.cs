using server;
using server.Application;
using server.Filters;
using server.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRouting(o => o.LowercaseUrls = true);
builder.Services.AddMvc(f => f.Filters.Add(typeof(ExceptionFilter)));

builder.Services.AddApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();