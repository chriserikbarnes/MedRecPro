
using MedRecPro.Configuration;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Security;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using MedRecPro.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using RazorLight;
using System.Reflection;
using System.Text.Json.Serialization;

string? connectionString;
string? googleClientId;
string? googleClientSecret;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
bool ignoreEmptyCollections;

// Set the static configuration for User class immediately
User.SetConfiguration(configuration);

// Access the connection string
connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                   ?? builder.Configuration.GetSection("Dev:DB:Connection")?.Value;

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string is not configured.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
     .LogTo(Console.WriteLine, LogLevel.Error));

builder.Services.Configure<ClaudeApiSettings>(builder.Configuration.GetSection("ClaudeApiSettings"));

builder.Services.Configure<MedRecPro.Models.ComparisonSettings>(builder.Configuration.GetSection("ComparisonSettings"));

googleClientId = builder.Configuration["Authentication:Google:ClientId"];

googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("appSettings"));

Boolean.TryParse(builder.Configuration["IgnoreEmptyObjectsWhenSerializing"], out ignoreEmptyCollections);

builder.Services.AddHttpContextAccessor();

// --- Custom Services ---
builder.Services.AddScoped<UserDataAccess>();

builder.Services.AddHttpClient<IClaudeApiService, ClaudeApiService>();

builder.Services.AddScoped<IComparisonService, ComparisonService>();

builder.Services.AddScoped<IClaudeApiService, ClaudeApiService>();

builder.Services.AddUserLogger(); // custom service

builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

builder.Services.AddScoped<ActivityLogActionFilter>();

builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddScoped(typeof(Repository<>), typeof(Repository<>));

builder.Services.AddScoped<SplXmlParser>();

builder.Services.AddScoped<SplImportService>();

builder.Services.AddScoped<SplDataService>();

builder.Services.AddTransient<StringCipher>();

if (builder.Configuration.GetValue<bool>("FeatureFlags:BackgroundProcessingEnabled", true))
    builder.Services.AddSingleton<IBackgroundTaskQueueService, BackgroundTaskQueueService>();

builder.Services.AddSingleton<IOperationStatusStore, InMemoryOperationStatusStore>();

builder.Services.AddHostedService<ZipImportWorkerService>();

builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

builder.Services.AddSingleton<IDictionaryUtilityService, DictionaryUtilityService>();

builder.Services.AddHostedService<DemoModeService>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDocumentRenderingServices(options =>
{
    options.EnablePerformanceLogging = true;
    options.CacheTemplates = true;
    options.MaxConcurrentOperations = Environment.ProcessorCount;
});

#region Session Configuration

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".MedRecPro.Session";
});

#endregion

#region User and Authentication
// --- ASP.NET Core Identity ---
// This registers UserManager, SignInManager, RoleManager, IPasswordHasher,
// and also calls services.AddAuthentication().AddIdentityCookies() internally,
// which sets up schemes like IdentityConstants.ApplicationScheme.
builder.Services.AddIdentity<User, IdentityRole<long>>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    // ApplicationCookie will be configured further below using ConfigureApplicationCookie
    // or by chaining AddCookie if preferred, but AddIdentity sets it up initially.
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// --- Authentication Configuration ---
// AddIdentity has already called AddAuthentication() and added cookie schemes.
// Now we retrieve the builder to add other schemes and further configure cookies.
// Or, you can use services.ConfigureApplicationCookie for cookie specific settings.

// Option 1: Chaining from services.AddAuthentication() (cleaner if AddIdentity didn't already call it, but it does)
// builder.Services.AddAuthentication(options => { ... }).AddCookie().AddGoogle()...

// Option 2: Configuring cookies specifically and adding other schemes
// This approach is often clearer when working with AddIdentity.

builder.Services.ConfigureApplicationCookie(options =>
{
    // These settings configure the IdentityConstants.ApplicationScheme cookie
    options.LoginPath = "/api/auth/login";
    options.AccessDeniedPath = "/api/auth/accessdenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; },
        OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; }
    };
});

// Now, add other authentication schemes like Google and your custom BasicAuthentication.
// We need to get an AuthenticationBuilder instance. Since AddIdentity calls AddAuthentication,
// we can call it again but mainly to get the builder for chaining.
// Alternatively, configure schemes one by one if AddAuthentication() is problematic.
// Let's try to get the builder and add schemes:
builder.Services.AddAuthentication(options =>
{
    // Set default schemes if not already adequately set by AddIdentity
    // AddIdentity usually sets DefaultScheme to IdentityConstants.ApplicationScheme
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme; // Can be overridden by specific challenges
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme; // For external logins
})
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", o => { /* Configure Basic Auth if needed */ })
    .AddGoogle(options =>
    {
        if (string.IsNullOrWhiteSpace(googleClientId) || string.IsNullOrWhiteSpace(googleClientSecret))
        {
            Console.WriteLine("Google ClientId or ClientSecret not configured. Google authentication will be disabled.");
            return;
        }
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.SaveTokens = true;
    });
#endregion

// Register our custom IPasswordHasher for MedRecPro.Models.User.
// AddIdentity<User,...> already registers IPasswordHasher<User>.
// This line is only needed if your PasswordHasher<User> is a *different* implementation
// than the one Identity would register by default for your User type.
// If it's just using the standard Identity password hashing algorithm, this is redundant.
// Assuming MedRecPro.Models.PasswordHasher<User> is your specific implementation.
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// --- Authorization ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BasicAuthPolicy", new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("BasicAuthentication") // Ensure this matches the scheme name above
        .RequireAuthenticatedUser()
        .Build());

    // Example: Default policy requiring any authenticated user
    // options.DefaultPolicy = new AuthorizationPolicyBuilder()
    //    .RequireAuthenticatedUser()
    //    .Build();
});

#region Ignore Empty Fields When Serializing
// Configure JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
    options.SerializerOptions.WriteIndented = true; // Optional: for readable JSON
});

// For controllers/API endpoints, also configure:
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
    options.SerializerOptions.WriteIndented = true;
});

#endregion

#region Newtonsoft Options
builder.Services.AddControllers()
.AddNewtonsoftJson(options =>
{
    // Ignore null values
    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;

    // Ignore default values (empty collections, default primitives)
    options.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Ignore;

    // Pretty print JSON
    options.SerializerSettings.Formatting = Formatting.Indented;

    // Custom configuration - ignore empty collections and objects
    if (ignoreEmptyCollections)
        options.SerializerSettings.ContractResolver = new ConfigurableIgnoreEmptyContractHelper(
            ignoreEmptyCollections: true,
            ignoreEmptyObjects: true);
    else
        options.SerializerSettings.ContractResolver = new ConfigurableIgnoreEmptyContractHelper(
            ignoreEmptyCollections: false,
            ignoreEmptyObjects: false);
});
#endregion

#region Swagger Documentation
builder.Services.AddSwaggerGen(c =>
{
    #region environment configuration
#if DEBUG || DEV
    var environment = "Dev";
    var serverName = "Localhost";
#elif RELEASE
    var environment = "Prod";
    var serverName = "ProdHost";
#endif
    #endregion

    #region demo mode detection
    // Check if demo mode is enabled from configuration
    var configuration = builder.Configuration;
    var demoModeEnabled = configuration.GetValue<bool>("DemoModeSettings:Enabled", false);
    var demoRefreshInterval = configuration.GetValue<int>("DemoModeSettings:RefreshIntervalMinutes", 60);
    var version = configuration.GetValue<string>("Version");

    // Build demo mode warning banner if enabled
    var demoModeWarning = demoModeEnabled
        ? $@"
---
## ⚠️ **DEMO MODE ACTIVE** ⚠️
**This system is running in DEMO MODE.**
- Database is automatically truncated every **{demoRefreshInterval} minutes**
- User authentication and activity logs are preserved
- All other data will be periodically removed
- DO NOT use for production data
---
"
        : string.Empty;
    #endregion

    c.DocumentFilter<IncludeLabelNestedTypesDocumentFilter>();

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = $"{version}",
        Title = "MedRecPro API",
        Description = $@"
{demoModeWarning}

This API provides a REST interface for the SQL Server {environment} environment ({serverName}).
It is designed to manage Structured Product Labeling (SPL) data based on the ICH7 SPL Implementation Guide (December 2023).
Reference: https://www.fda.gov/media/84201/download?attachment

---

## 📋 General Information

### Data Access
The API utilizes a generic repository pattern (`Repository<T>`) implemented in `MedRecPro.DataAccess` for database operations. 
It assumes table names match class names and primary keys follow the `[ClassName]ID` naming convention. 
EntityFramework is used for object-relational mapping.

### Data Models
Data structures corresponding to the database tables (e.g., `Document`, `Organization`) are defined in 
`MedRecPro.Models` (`Labels.cs`).

### Database Schema
The underlying database schema is defined in `MedRecPro.sql`.

### Security
All primary/foriegn keys are encrypted using a secure cipher. Encrypted IDs must be provided in requests and will be 
returned in responses for security purposes.

---

## 🔐 Authentication & Authorization (`/api/Auth`)

The authentication system uses ASP.NET Core Identity with cookie-based authentication and supports external OAuth providers.

### **External Login Endpoints**
* **`GET /api/auth/login/{{provider}}`**: Initiates external login flow (e.g., Google, Microsoft)
  * **Parameters:** `provider` (string, path) - The authentication provider name
  * **Response:** Redirects to provider's login page
  * **Example:** `GET /api/auth/login/Google`

* **`GET /api/auth/external-logincallback`**: OAuth callback endpoint
  * **Parameters:** 
    - `returnUrl` (string, query, optional) - URL to redirect after successful login
    - `remoteError` (string, query, optional) - Error message from provider
  * **Responses:**
    - Redirects to returnUrl on success
    - Redirects to login failure page on error
  * **Note:** This endpoint is automatically called by the OAuth provider

### **Session Management**
* **`GET /api/auth/user`**: Retrieves current authenticated user information
  * **Authorization:** Required (must be logged in)
  * **Responses:**
    - `200 OK` - Returns user ID (encrypted), name, and claims
    - `401 Unauthorized` - If not authenticated

* **`POST /api/auth/logout`**: Logs out the current user
  * **Authorization:** Required
  * **Response:** `200 OK` - Confirmation message

### **Error Handling Endpoints**
* **`GET /api/auth/loginfailure`**: Handles login failures
  * **Parameters:** `message` (string, query, optional) - Error message
  * **Response:** `400 Bad Request` - Error details

* **`GET /api/auth/lockout`**: Handles account lockout
  * **Response:** `403 Forbidden` - Account locked message

* **`GET /api/auth/accessdenied`**: Handles authorization failures
  * **Response:** `403 Forbidden` - Access denied message

### **Swagger Helper Endpoints**
* **`GET /api/auth/external-login`**: Swagger OAuth2 authorization URL placeholder
* **`POST /api/auth/token-placeholder`**: Swagger OAuth2 token URL placeholder

---

## 📄 Label CRUD API (`/api/Labels`)

This controller manages SPL Label Section entities with dynamic routing based on section type.
Each section corresponds to a nested class within the Label data model (e.g., Document, Organization, ActiveMoiety).

### **Metadata & Documentation**
* **`GET /api/labels/sectionMenu`**: Retrieves list of all available label sections
  * **Response:** `200 OK` - Array of section names
  * **Use Case:** Discover available sections for dynamic UI construction

* **`GET /api/labels/{{menuSelection}}/documentation`**: Get schema documentation for a specific section
  * **Parameters:** `menuSelection` (string, path) - The section name (e.g., ""Document"", ""Organization"")
  * **Response:** `200 OK` - JSON schema and property descriptions
  * **Example:** `GET /api/labels/Document/documentation`

### **Data Retrieval**
* **`GET /api/labels/section/{{menuSelection}}`**: Get all records for a specific section (paginated)
  * **Parameters:**
    - `menuSelection` (string, path) - Section name
    - `pageNumber` (int, query, optional) - Default: 1
    - `pageSize` (int, query, optional) - Default: 10
  * **Response:** `200 OK` - Paginated list of records

* **`GET /api/labels/{{menuSelection}}/{{encryptedId}}`**: Get a specific record by encrypted ID
  * **Parameters:**
    - `menuSelection` (string, path) - Section name
    - `encryptedId` (string, path) - Encrypted primary key
  * **Responses:**
    - `200 OK` - The requested record
    - `404 Not Found` - Record doesn't exist

* **`GET /api/labels/single/{{documentGuid}}`**: Get a complete document by GUID
  * **Parameters:** `documentGuid` (GUID, path) - The document's unique identifier
  * **Response:** `200 OK` - Complete document with all related sections
  * **Example:** `GET /api/labels/single/123e4567-e89b-12d3-a456-426614174000`

* **`GET /api/labels/complete/{{pageNumber}}/{{pageSize}}`**: Get all complete documents (paginated)
  * **Parameters:**
    - `pageNumber` (int, path, optional) - Default: 1
    - `pageSize` (int, path, optional) - Default: 10
  * **Response:** `200 OK` - Paginated list of complete documents

### **Data Modification**
* **`POST /api/labels/{{menuSelection}}`**: Create a new record
  * **Parameters:** `menuSelection` (string, path) - Section name
  * **Request Body:** Record object (ID should be omitted or 0)
  * **Responses:**
    - `201 Created` - Returns created record with assigned encrypted ID
    - `400 Bad Request` - Invalid data

* **`PUT /api/labels/{{menuSelection}}/{{encryptedId}}`**: Update an existing record
  * **Parameters:**
    - `menuSelection` (string, path) - Section name
    - `encryptedId` (string, path) - Encrypted ID of record to update
  * **Request Body:** Updated record object (ID must match encryptedId)
  * **Responses:**
    - `204 No Content` - Update successful
    - `400 Bad Request` - ID mismatch or invalid data
    - `404 Not Found` - Record doesn't exist

* **`DELETE /api/labels/{{menuSelection}}/{{encryptedId}}`**: Delete a record
  * **Parameters:**
    - `menuSelection` (string, path) - Section name
    - `encryptedId` (string, path) - Encrypted ID of record to delete
  * **Responses:**
    - `204 No Content` - Deletion successful
    - `404 Not Found` - Record doesn't exist

### **Import/Export Operations**
* **`POST /api/labels/import`**: Import SPL data from ZIP file
  * **Request Body:** Multipart form with ZIP file containing XML files
  * **Response:** `202 Accepted` - Returns operation ID for progress tracking
  * **Note:** This is a long-running background operation

* **`GET /api/labels/import/progress/{{operationId}}`**: Check import progress
  * **Parameters:** `operationId` (GUID, path) - Operation identifier from import response
  * **Response:** `200 OK` - Progress status and details

* **`GET /api/labels/generate/{{documentGuid}}/{{minify}}`**: Generate SPL XML from document
  * **Parameters:**
    - `documentGuid` (GUID, path) - Document identifier
    - `minify` (bool, path) - Whether to minify the XML output
  * **Response:** `200 OK` - Generated SPL XML

### **Analysis & Comparison**
* **`POST /api/labels/comparison/analysis/{{documentGuid}}`**: Analyze document against FDA template
  * **Parameters:** `documentGuid` (GUID, path) - Document to analyze
  * **Response:** `202 Accepted` - Returns operation ID for progress tracking

* **`GET /api/labels/comparison/analysis/{{documentGuid}}`**: Get cached analysis results
  * **Parameters:** `documentGuid` (GUID, path) - Document identifier
  * **Response:** `200 OK` - Analysis report (if available in cache)

* **`GET /api/labels/comparison/progress/{{operationId}}`**: Check analysis progress
  * **Parameters:** `operationId` (GUID, path) - Operation identifier
  * **Response:** `200 OK` - Progress status and analysis results when complete

---

## 👥 User Management API (`/api/Users`)

Manages user accounts, authentication, activity tracking, and administrative operations.

### **User Profile Operations**
* **`GET /api/users/{{encryptedUserId}}`**: Get user by encrypted ID
  * **Parameters:** `encryptedUserId` (string, path) - Encrypted user ID
  * **Authorization:** Required
  * **Responses:**
    - `200 OK` - User profile
    - `404 Not Found` - User doesn't exist

* **`GET /api/users`**: Get all users (paginated)
  * **Authorization:** Admin role required
  * **Parameters:**
    - `pageNumber` (int, query, optional) - Default: 1
    - `pageSize` (int, query, optional) - Default: 10
  * **Response:** `200 OK` - Paginated user list

* **`GET /api/users/byemail`**: Find user by email address
  * **Parameters:** `email` (string, query) - Email address to search
  * **Authorization:** Required
  * **Responses:**
    - `200 OK` - User profile
    - `404 Not Found` - User not found

* **`GET /api/users/me`**: Get current authenticated user's profile
  * **Authorization:** Required
  * **Response:** `200 OK` - Current user's profile with encrypted ID

* **`PUT /api/users/{{encryptedUserId}}/profile`**: Update user profile
  * **Parameters:** `encryptedUserId` (string, path) - Encrypted user ID
  * **Request Body:** Updated user profile object
  * **Authorization:** Required (must be the user or admin)
  * **Responses:**
    - `204 No Content` - Update successful
    - `400 Bad Request` - Invalid data
    - `404 Not Found` - User doesn't exist

* **`DELETE /api/users/{{encryptedUserId}}`**: Delete user account
  * **Parameters:** `encryptedUserId` (string, path) - Encrypted user ID
  * **Authorization:** Admin role required
  * **Responses:**
    - `204 No Content` - Deletion successful
    - `404 Not Found` - User doesn't exist

### **Activity Tracking**
* **`GET /api/users/user/{{encryptedUserId}}/activity`**: Get user's activity log
  * **Parameters:**
    - `encryptedUserId` (string, path) - Encrypted user ID
    - `pageNumber` (int, query, optional) - Default: 1
    - `pageSize` (int, query, optional) - Default: 50
  * **Authorization:** Required (must be the user or admin)
  * **Response:** `200 OK` - Paginated activity log

* **`GET /api/users/user/{{encryptedUserId}}/activity/daterange`**: Get activity within date range
  * **Parameters:**
    - `encryptedUserId` (string, path) - Encrypted user ID
    - `startDate` (DateTime, query) - Start of date range
    - `endDate` (DateTime, query) - End of date range
    - `pageNumber` (int, query, optional)
    - `pageSize` (int, query, optional)
  * **Authorization:** Required (must be the user or admin)
  * **Response:** `200 OK` - Filtered activity log

* **`GET /api/users/endpoint-stats`**: Get API endpoint usage statistics
  * **Parameters:**
    - `startDate` (DateTime, query, optional) - Start date for stats
    - `endDate` (DateTime, query, optional) - End date for stats
  * **Authorization:** Admin role required
  * **Response:** `200 OK` - Aggregated endpoint usage data

### **Authentication** (Legacy/Alternative)
* **`POST /api/users/signup`**: Create new user account
  * **Request Body:** User registration object (username, email, password)
  * **Responses:**
    - `201 Created` - User created successfully
    - `400 Bad Request` - Validation errors or user already exists

* **`POST /api/users/authenticate`**: Authenticate user with credentials
  * **Request Body:** Login credentials (username/email and password)
  * **Responses:**
    - `200 OK` - Authentication successful, returns encrypted user ID
    - `401 Unauthorized` - Invalid credentials
  * **Note:** External OAuth login via `/api/auth/login/{{provider}}` is recommended

### **Administrative Operations**
* **`PUT /api/users/admin-update`**: Admin bulk update user properties
  * **Request Body:** Array of user updates
  * **Authorization:** Admin role required
  * **Response:** `200 OK` - Update results

* **`POST /api/users/rotate-password`**: Rotate user password
  * **Request Body:** Current and new password
  * **Authorization:** Required (must be the user)
  * **Responses:**
    - `200 OK` - Password updated
    - `400 Bad Request` - Invalid current password

---

## 🗄️ Database Caching

The system implements both timer-based and managed caching for database requests to optimize performance.
Retrieving data from cache is significantly faster than querying the database directly.

When you need to ensure fresh data from the database, use the `/API/Settings/ClearManagedCache` endpoint 
to clear the managed cache before making your request.

---

## 📝 Notes

* All endpoints that return or accept IDs use **encrypted IDs** for security
* Timestamps are stored and returned in **UTC**
* Pagination is supported on list endpoints with configurable page size
* All POST/PUT operations validate input data and return detailed error messages
* Background operations (import, analysis) return operation IDs for async progress tracking
* Activity logging captures all API usage for audit purposes

For detailed examples of request/response formats, refer to the XML comments within each controller file.
"
    });

    c.AddSecurityDefinition("BasicAuthentication", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Basic",
        In = ParameterLocation.Header,
        Description = "Basic Authorization header using the Basic scheme. Example: \"Authorization: Basic {base64(email:password)}\""
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "BasicAuthentication"
                }
            },
            Array.Empty<string>()
        }
    });
    // Set the comments path for the Swagger JSON and UI.
    var dataModelsXmlFile = $"{typeof(MedRecPro.Models.Label).Assembly.GetName().Name}.xml";
    var dataModelsXmlPath = Path.Combine(AppContext.BaseDirectory, dataModelsXmlFile);
    if (File.Exists(dataModelsXmlPath))
    {
        c.IncludeXmlComments(dataModelsXmlPath);

        // Optional: To include comments from <inheritdoc/> tags
        c.IncludeXmlComments(dataModelsXmlPath, includeControllerXmlComments: true);
    }
    else
    {
        // Log a warning if the XML file is missing, as summaries won't appear
        Console.WriteLine($"Warning: XML documentation file not found for DataModels: {dataModelsXmlPath}");
    }

    var apiXmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var apiXmlPath = Path.Combine(AppContext.BaseDirectory, apiXmlFile);
    if (File.Exists(apiXmlPath))
    {
        c.IncludeXmlComments(apiXmlPath);
        c.IncludeXmlComments(apiXmlPath, includeControllerXmlComments: true);
    }
    else
    {
        Console.WriteLine($"Warning: XML documentation file not found for API: {apiXmlPath}");
    }
});
#endregion

#region File Limits

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue; // 2GB
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue; // 2GB
});

#endregion

#region View Configuration

// Enable ASP.NET Core Razor Views with Activity Logging Filter
if (builder.Configuration.GetValue<bool>("FeatureFlags:BackgroundProcessingEnabled", true))
    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<ActivityLogActionFilter>();
    });
else
    builder.Services.AddControllersWithViews();

// RazorLight for programmatic templates (after your existing custom services)
builder.Services.AddSingleton<IRazorLightEngine>(serviceProvider =>
{
var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();

return new RazorLightEngineBuilder()
    .UseFileSystemProject(Path.Combine(environment.ContentRootPath, "Views"))
    .UseEmbeddedResourcesProject(typeof(Program))
    .UseMemoryCachingProvider()
    .AddMetadataReferences(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(Program).Assembly.Location))
    .AddMetadataReferences(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(User).Assembly.Location))
    .AddMetadataReferences(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(ApplicationDbContext).Assembly.Location))
    .EnableDebugMode(environment.IsDevelopment())
    .Build();
});

// View rendering service for ASP.NET Core views
builder.Services.AddScoped<IViewRenderService, ViewRenderService>();
#endregion

var app = builder.Build();

// ---Middleware Pipeline---
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MedRecPro API V1");
        c.ConfigObject.AdditionalItems["operationsSorter"] = "method";
        c.ConfigObject.AdditionalItems["tagsSorter"] = "alpha";
        c.RoutePrefix = "swagger"; // Access Swagger UI at /swagger
    });
}
else
{
    app.UseExceptionHandler("/Error"); // You'll need an Error handling page/endpoint
    app.UseHsts();
}

// Configure static files with CORS for XSL files
// Configure static files with proper CORS and content types
// Serve stylesheets from Views/Stylesheets at /stylesheets URL path
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(app.Environment.ContentRootPath, "Views", "Stylesheets")),
    RequestPath = "/stylesheets",
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name.ToLowerInvariant();

        // Handle XSL files
        if (path.EndsWith(".xsl"))
        {
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET");
            ctx.Context.Response.Headers.Append("Content-Type", "application/xslt+xml; charset=utf-8");
        }
        // Handle XML files
        else if (path.EndsWith(".xml"))
        {
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            ctx.Context.Response.Headers.Append("Content-Type", "application/xml; charset=utf-8");
        }
        // Handle CSS files
        else if (path.EndsWith(".css"))
        {
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            ctx.Context.Response.Headers.Append("Content-Type", "text/css; charset=utf-8");
        }
    }
});

app.UseHttpsRedirection();
app.UseRouting();

// Authentication & Authorization must come after UseRouting and before UseEndpoints
app.UseAuthentication(); // Enables authentication capabilities
app.UseAuthorization();  // Enables authorization capabilities

app.MapControllers();

Util.Initialize(httpContextAccessor: app.Services.GetRequiredService<IHttpContextAccessor>(),
    encryptionService: app.Services.GetRequiredService<IEncryptionService>(),
    dictionaryUtilityService: app.Services.GetRequiredService<IDictionaryUtilityService>());

app.Run();