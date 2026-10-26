using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Rtmi.Api;
using Rtmi.Api.Hubs;
using Rtmi.Api.Services;
using Rtmi.Api.Simulation;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Urls"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? "http://localhost:5080");

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddSingleton<EventStore>();
builder.Services.AddSingleton<EventBus>();
builder.Services.AddSingleton<StatsEngine>();
builder.Services.AddSingleton<MomentumCalculator>();
builder.Services.AddSingleton<InsightStore>();
builder.Services.AddSingleton<InsightValidator>();
builder.Services.AddSingleton<TokenCredential>(_ =>
    builder.Environment.IsDevelopment()
        ? new AzureCliCredential()
        : new DefaultAzureCredential());
builder.Services.AddSingleton<IFoundryAgentInvoker, FoundryAgentInvoker>();
builder.Services.AddSingleton<IMatchBroadcaster, MatchBroadcaster>();
builder.Services.AddSingleton<MatchSimulator>();
builder.Services.AddSingleton<IMatchControl>(services => services.GetRequiredService<MatchSimulator>());
builder.Services.AddHostedService(services => services.GetRequiredService<MatchSimulator>());
builder.Services.AddHostedService<InsightOrchestrator>();

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHub<MatchHub>("/hubs/match");
app.Run();

public partial class Program;
