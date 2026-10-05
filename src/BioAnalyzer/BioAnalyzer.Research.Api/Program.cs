using BioAnalyzer.Research.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddSecureConfiguration(builder.Configuration);
builder.Services.AddResearchApiDependencies(builder.Configuration);
builder.Services.AddResearchApiAuthentication(builder.Configuration, builder.Environment);

var app = builder.Build();

var authState = app.Services.GetRequiredService<ApiAuthenticationRuntimeState>();
if (!authState.Enforced)
{
    app.Logger.LogWarning(
        "Research.Api authentication is NOT enforced (dev bypass / missing AzureAd and API key). " +
        "Configure AzureAd__TenantId + AzureAd__ClientId (Entra JWT) and/or ApiAuthentication__ApiKey before exposing this service.");
}
else
{
    app.Logger.LogInformation(
        "Research.Api authentication enforced. JwtEnabled={JwtEnabled} ApiKeyEnabled={ApiKeyEnabled} ApiKeyHeader={HeaderName}",
        authState.JwtEnabled,
        authState.ApiKeyEnabled,
        authState.HeaderName);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

//app.UseHttpsRedirection();
app.UseAuthentication();
app.MapDefaultEndpoints();

app.UseAuthorization();

app.MapControllers();

app.Run();
