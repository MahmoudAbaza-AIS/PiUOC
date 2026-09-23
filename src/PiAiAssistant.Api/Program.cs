using System.Text.Json;
using System.Text.Json.Serialization;
using PiAiAssistant.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

static void ConfigureJson(JsonSerializerOptions options)
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.Converters.Add(new JsonStringEnumConverter());
}

builder.Services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));
builder.Services.AddControllers().AddJsonOptions(o => ConfigureJson(o.JsonSerializerOptions));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

var app = builder.Build();
await app.Services.InitializeInfrastructureAsync();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();

public partial class Program;
