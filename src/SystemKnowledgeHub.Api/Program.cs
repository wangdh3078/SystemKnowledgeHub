using System.Text.Json;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Application;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Persistence;
using SystemKnowledgeHub.Api.Features.BusinessRules.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Persistence;
using SystemKnowledgeHub.Api.Features.Evidence.Application;
using SystemKnowledgeHub.Api.Features.StatusProgression.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Systems.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application;
using SystemKnowledgeHub.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddKnowledgeHubPersistence(builder.Configuration, builder.Environment);
builder.Services.AddScoped<BusinessFunctionQueries>();
builder.Services.AddScoped<BusinessFunctionService>();
builder.Services.AddScoped<BusinessRuleQueries>();
builder.Services.AddScoped<BusinessRuleService>();
builder.Services.AddScoped<DatabaseKnowledgeQueries>();
builder.Services.AddScoped<EvidenceSubjectResolver>();
builder.Services.AddScoped<EvidenceQueries>();
builder.Services.AddScoped<EvidenceService>();
builder.Services.AddScoped<KnowledgeStatusPolicy>();
builder.Services.AddScoped<KnowledgeStatusService>();
builder.Services.AddScoped<RelationshipEndpointPolicy>();
builder.Services.AddScoped<RelationshipTargetResolver>();
builder.Services.AddScoped<RelationshipQueries>();
builder.Services.AddScoped<RelationshipService>();
builder.Services.AddScoped<SystemQueries>();
builder.Services.AddScoped<SystemService>();
builder.Services.AddScoped<UnknownItemQueries>();
builder.Services.AddScoped<UnknownItemService>();
builder.Services.AddScoped<KnowledgeResolutionService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteDevelopment", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await DatabaseKnowledgeDevelopmentData.InitializeAsync(app.Services);
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
    await BusinessFunctionDevelopmentData.SeedAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseCors("ViteDevelopment");
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();

public partial class Program;
