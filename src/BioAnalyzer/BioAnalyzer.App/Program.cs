using BioAnalyzer.App.Components;
using BioAnalyzer.App.Infrastructure;
using BioAnalyzer.App.State;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddAuthorization(config =>
{
    var authenticationPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    config.DefaultPolicy = authenticationPolicy;
});


builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.AddSecureConfiguration(builder.Configuration);
builder.Services.AddBioAnalyzerAppDependencies(builder.Configuration);
builder.Services.AddBioAnalyzerAuthentication(builder.Configuration);
builder.Services.AddScoped<ApplicationState>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

//app.UseHttpsRedirection();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();