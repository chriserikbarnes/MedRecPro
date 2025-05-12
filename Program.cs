using System.Reflection;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Google.Apis.Auth.AspNetCore3;
using MedRecPro.Helpers; // Namespace for StringCipher, AppSettings etc.
using Microsoft.AspNetCore.Authentication.Cookies;
using MedRecPro.Data; // Namespace for ApplicationDbContext
using Microsoft.AspNetCore.Identity;
using MedRecPro.Models; // Namespace for User model (if needed directly)
using System.Security.Cryptography;
using MedRecPro.DataAccess;
using Microsoft.AspNet.Identity; // For HashAlgorithmName if needed elsewhere

string? connectionString, googleClientId, googleClientSecret;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration; // Get configuration

// Access the connection string
connectionString = builder.Configuration.GetConnectionString("DefaultConnection") // Recommend using GetConnectionString
                   ?? builder.Configuration.GetSection("Dev:DB:Connection")?.Value; // Fallback if needed

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string is not configured.");
}


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

googleClientId = builder.Configuration["Authentication:Google:ClientId"]; // Simplified access
googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]; // Simplified access

// Bind AppSettings (if used)
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("appSettings"));

// No need to register AppSettings itself unless used directly as a service
builder.Services.AddHttpContextAccessor();
builder.Services.AddUserLogger(); // Assuming this is your custom logger setup

// Register StringCipher for DI
// Transient is usually suitable for stateless utility classes like this.
builder.Services.AddTransient<StringCipher>();

// Register other services that might use User, IConfiguration, StringCipher
// Example: builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddEndpointsApiExplorer();

// --- Identity ---
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// --- Custom Services ---
builder.Services.AddScoped<IUserDataAccess, UserDataAccess>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// --- Authentication ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
})
    .AddCookie(options =>
    {
        options.LoginPath = "/api/auth/login";
        options.AccessDeniedPath = "/api/auth/accessdenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        // Configure SameSite and SecurePolicy based on requirements
        options.Cookie.SameSite = SameSiteMode.Lax; // Lax is often safer than None unless cross-site needed
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Requires HTTPS

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; },
            OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; }
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = googleClientId ?? throw new InvalidOperationException("Google ClientId not configured.");
        options.ClientSecret = googleClientSecret ?? throw new InvalidOperationException("Google ClientSecret not configured.");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.SaveTokens = true;
    });

builder.Services.AddAuthorization();
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


var app = builder.Build();


// ---Middleware Pipeline-- -
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // More detailed errors in dev
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MedRecPro API V1");
        c.RoutePrefix = "swagger"; 
        // Removed OAuth Client ID/App Name - handled by flows/redirects usually
    });
}
else
{
    app.UseExceptionHandler("/Error"); // Use a proper error handling page/endpoint
    app.UseHsts(); // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
}

app.UseHttpsRedirection(); // Redirect HTTP to HTTPS

app.UseRouting(); // Marks the position in the middleware pipeline where routing decisions are made

// Authentication & Authorization must come after UseRouting and before UseEndpoints
app.UseAuthentication(); // Attempts to authenticate the user before they access secure resources.
app.UseAuthorization(); // Authorizes a user to access secure resources.

app.MapControllers(); // Maps attribute-routed controllers

app.Run();
