/**************************************************************/
// MedRecPro API Application.
//
// IMPORTANT: When deploying to production, the caching settings
// at https://dash.cloudflare.com/{secret}/medrecpro.com/caching/configuration must be purged.
// Failing to purge will result in errors when loading swagger docs.
/**************************************************************/

using MedRecPro.Configuration;
using MedRecPro.Helpers;
using MedRecPro.Middleware;
using MedRecPro.Models;
using MedRecPro.Security;
using MedRecPro.Service.Common;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Set the static configuration for User class immediately.
User.SetConfiguration(configuration);

builder.AddMedRecProKeyVault();
builder.AddMedRecProDataAccess();
builder.AddMedRecProConfigurationSettings();
builder.Services.AddMedRecProPlatformServices(builder.Configuration);
builder.Services.AddMedRecProAi();
builder.Services.AddMedRecProUserServices();
builder.Services.AddMedRecProImport();
builder.Services.AddMedRecProBackgroundServices(builder.Configuration);
builder.Services.AddMedRecProRendering();
builder.Services.AddMedRecProSession();
builder.Services.AddMedRecProAuth(builder.Configuration);
builder.Services.AddMedRecProApiControllers(builder.Configuration);
builder.Services.AddMedRecProSwagger(builder.Configuration);
builder.Services.AddMedRecProRequestLimits();
builder.Services.AddMedRecProViews(builder.Configuration);

var app = builder.Build();

app.LogMedRecProStartupDiagnostics();

/**************************************************************/
// ---Middleware Pipeline---
app.UseMedRecProExceptionHandling();

// Tarpit middleware: progressively delays 404 responses from repeat offenders.
app.UseTarpitMiddleware();

app.UseMedRecProSwagger();

app.UseMedRecProSplStaticFiles();

/**************************************************************/
app.UseHttpsRedirection();

app.UseRouting();

app.UseMedRecProCors();

// Authentication & Authorization must come after UseRouting and before UseEndpoints.
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

Util.Initialize(httpContextAccessor: app.Services.GetRequiredService<IHttpContextAccessor>(),
    encryptionService: app.Services.GetRequiredService<IEncryptionService>(),
    dictionaryUtilityService: app.Services.GetRequiredService<IDictionaryUtilityService>(),
    logger: app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MedRecPro.Helpers.Util"));

#if !DEBUG
// Add a root endpoint
app.MapGet("/", () => Results.Ok(new
{
    name = "MedRecPro API",
    version = configuration.GetValue<string>("Version"),
    status = "running",
    swagger = "/swagger",
    documentation = "https://www.medrecpro.com/api/swagger"
}));
#endif

app.Run();

/**************************************************************/
/// <summary>
/// Exposes the top-level application entry point to the integration-test host.
/// </summary>
/// <remarks>
/// This partial declaration does not alter startup, middleware, route mappings, or the existing
/// Debug/Release compiler directives; it only makes <c>WebApplicationFactory&lt;Program&gt;</c> possible.
/// </remarks>
public partial class Program { }
