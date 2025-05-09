using System.Reflection;  // Import System.Reflection to enable accessing metadata about assemblies.
using Microsoft.OpenApi.Models;  // Import OpenAPI models for Swagger/OpenAPI configuration.
using Microsoft.EntityFrameworkCore;  // Import Entity Framework Core for database context and migrations.

using Microsoft.AspNetCore.Mvc;  // For IUrlHelper and IUrlHelperFactory
using Microsoft.AspNetCore.Mvc.Infrastructure;  // For IActionContextAccessor
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;  // For service registration extensions

using Google.Apis.Auth.AspNetCore3;
using MedRecPro.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using MedRecPro.Data;
using Microsoft.AspNetCore.Identity;

string? connectionString, googleClientId, googleClientSecret;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

// Access the connection string
connectionString = builder.Configuration.GetSection("Dev:DB:Connection")?.Value;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)); // Or UseSqlServer

googleClientId = builder.Configuration
    .GetSection("Authentication:Google:ClientId")
    ?.Value
    ?.ToString();

googleClientSecret = builder.Configuration
    .GetSection("Authentication:Google:ClientSecret")
    ?.Value
    ?.ToString();

// Bind AppSettings from configuration to services
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("appSettings"));

// Register AppSettings as a transient service in the dependency injection container
builder.Services.AddTransient<AppSettings>();

// Register IHttpContextAccessor for accessing the HTTP context in services
builder.Services.AddHttpContextAccessor();

// Logging Configuration
builder.Services.AddUserLogger();  // Add custom user logging service

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Configure Identity options if needed (e.g., password requirements)
    options.SignIn.RequireConfirmedAccount = false; // Keep false for easier external login testing
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
    {
        // Use cookie authentication as the default scheme for the browser/Swagger UI
    options.DefaultScheme = IdentityConstants.ApplicationScheme; // Cookie Authentication
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme; // Challenge with cookie auth
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/api/auth/login"; // Redirect here if unauthorized (won't work directly for API, but useful for reference)
        options.AccessDeniedPath = "/api/auth/accessdenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Requires HTTPS

         //Prevent automatic redirects for API calls expecting 401/403
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    })
    .AddGoogle(options =>
    {
        // This is application client id for google
        options.ClientId = googleClientId ?? throw new InvalidOperationException("Google ClientId not configured.");
        // This is the secret used to validate the token
        options.ClientSecret = googleClientSecret ?? throw new InvalidOperationException("Google ClientSecret not configured.");
        // Request email and profile info
        options.Scope.Add("profile");
        options.Scope.Add("email");
        // Store tokens for potential later use (optional)
        options.SaveTokens = true;

    });


// --- Authorization ---
builder.Services.AddAuthorization(); // Add authorization services

// --- API Controllers ---
builder.Services.AddControllers();

#region swagger documentation
builder.Services.AddSwaggerGen(options =>
{

#if DEBUG || DEV
    var environment = "Dev";
    var serverName = "Localhost";

#elif RELEASE
    var environment = "Prod";
    var serverName = "ProdHost";
#endif

    // Configure Swagger for development environment
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "MedRecPro API",
        Description = $@"
This API provides a REST interface for the OGD SQL Server {environment} environment ({serverName}).
It is designed to manage Structured Product Labeling (SPL) data based on the ICH7 SPL Implementation Guide (December 2023).

## General Information

* **Data Access:** The API utilizes a generic repository pattern (`GenericRepository<T>`) implemented in `MedRecPro.DataAccess` for database operations. It assumes table names match class names and primary keys follow the `[ClassName]ID` naming convention. Dapper is used for object-relational mapping.
* **Data Models:** Data structures corresponding to the database tables (e.g., `Document`, `Organization`) are defined in `MedRecPro.DataModels` (`LabelClasses.cs`).
* **Database Schema:** The underlying database schema is defined in `MedRecPro-TableCreation.sql`.

## API Endpoints

### Documents (`/api/Documents`)

This controller manages `Document` entities, providing CRUD operations based on SPL metadata.

* **`GET /api/Documents`**: Retrieves all Document records.
    * **Response:** `200 OK` - Returns a list of documents.
* **`GET /api/Documents/id`**: Retrieves a specific Document record by its primary key (`DocumentID`).
    * **Parameters:** `id` (integer, path) - The `DocumentID` of the document to retrieve.
    * **Responses:**
        * `200 OK` - Returns the requested document.
        * `404 Not Found` - If the document with the specified ID is not found.
* **`POST /api/Documents`**: Creates a new Document record.
    * **Request Body:** A `Document` object (defined in `LabelClasses.cs`). `DocumentID` should be omitted or 0 as it is auto-generated.
    * **Responses:**
        * `201 Created` - Returns the newly created document, including its assigned `DocumentID`.
        * `400 Bad Request` - If the input document data is invalid.
* **`PUT /api/Documents/id`**: Updates an existing Document record.
    * **Parameters:** `id` (integer, path) - The `DocumentID` of the document to update.
    * **Request Body:** The updated `Document` object. The `DocumentID` in the body must match the `id` in the route.
    * **Responses:**
        * `204 No Content` - If the update was successful.
        * `400 Bad Request` - If the ID in the route doesn't match the ID in the body, or if the data is invalid.
        * `404 Not Found` - If the document with the specified ID is not found.
* **`DELETE /api/Documents/id;`**: Deletes a Document record by its ID.
    * **Parameters:** `id` (integer, path) - The `DocumentID` of the document to delete.
    * **Responses:**
        * `204 No Content` - If the deletion was successful.
        * `404 Not Found` - If the document with the specified ID is not found.

*(Examples for each endpoint can be found in the XML comments within `LabelController.cs`)*.

---

## Database Caching:
The system implements both timer-based and managed caching for database requests to optimize performance.
Retrieving data from cache is significantly faster than querying the database directly.
When you need to ensure fresh data from the database, use the `/REST/API/Utility/ClearManagedCache` endpoint to clear the managed cache before making your request."
    });


    // Define the OAuth2 scheme used for initiating the login flow via API endpoints
    // This doesn't directly talk to Google/MS/Apple from Swagger UI, but tells Swagger
    // how to trigger the API's login endpoints.
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            // We use Authorization Code flow triggered by our API endpoints
            AuthorizationCode = new OpenApiOAuthFlow
            {
                // These URLs point to OUR API which will then handle the external redirect
                // Adjust paths based on your AuthController implementation below
                AuthorizationUrl = new Uri("/api/auth/external-login", UriKind.Relative),
                TokenUrl = new Uri("/api/auth/token-placeholder", UriKind.Relative), // Placeholder - token handled by cookie/callback
                Scopes = new Dictionary<string, string>
                {
                    // Define scopes if needed for your API, not directly for external providers here
                    { "api.read", "Read access to the API" }
                }
            }
        },
        Description = "OAuth2 Authentication using external providers (Google, Microsoft, Apple)"
    });

    // Apply the security definition globally to all endpoints that require authorization
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2" // Must match the name defined in AddSecurityDefinition
                },
                Scheme = "oauth2",
                Name = "oauth2",
                In = ParameterLocation.Header
            },
            new List<string>() // List of required scopes (can be empty if not enforcing specific scopes here)
            // Example: new List<string> { "api.read" }
        }
    });
});

#endregion

builder.Services.AddSwaggerGen();

var app = builder.Build();


app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();


// Configure Swagger middleware for API documentation and OAuth2 authentication
app.UseSwagger();  // Enable Swagger middleware
app.UseSwaggerUI();

app.MapControllers();
app.Run();

