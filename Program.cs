
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication; // Required for AuthenticationBuilder
using System.Reflection;

using MedRecPro.Data; // Namespace for ApplicationDbContext
using MedRecPro.Helpers; // Namespace for StringCipher, AppSettings etc.
using MedRecPro.Service;
using MedRecPro.Models; // Namespace for User model
using MedRecPro.DataAccess;
using MedRecPro.Security; // Namespace for BasicAuthenticationHandler

using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerUI;

string? connectionString, googleClientId, googleClientSecret;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

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
    options.UseSqlServer(connectionString));

googleClientId = builder.Configuration["Authentication:Google:ClientId"];

googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("appSettings"));

builder.Services.AddHttpContextAccessor();

// --- Custom Services ---
builder.Services.AddScoped<UserDataAccess>();

builder.Services.AddUserLogger(); // custom service

builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddScoped(typeof(Repository<>), typeof(Repository<>));

builder.Services.AddScoped<SplXmlParser>(); 

builder.Services.AddScoped<SplImportService>();

builder.Services.AddTransient<StringCipher>();

builder.Services.AddEndpointsApiExplorer();

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

builder.Services.AddControllers();

#region Swagger Documentation
builder.Services.AddSwaggerGen(c =>
{
#if DEBUG || DEV
    var environment = "Dev";
    var serverName = "Localhost";

#elif RELEASE
    var environment = "Prod";
    var serverName = "ProdHost";
#endif

    c.DocumentFilter<IncludeLabelNestedTypesDocumentFilter>();

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "MedRecPro API",
        Description = $@"
This API provides a REST interface for the SQL Server {environment} environment ({serverName}).
It is designed to manage Structured Product Labeling (SPL) data based on the ICH7 SPL Implementation Guide (December 2023) https://www.fda.gov/media/84201/download?attachment.

## General Information

* **Data Access:** The API utilizes a generic repository pattern (`Repository<T>`) implemented in `MedRecPro.DataAccess` for database operations. It assumes table names match class names and primary keys follow the `[ClassName]ID` naming convention. EntityFramework is used for object-relational mapping.
* **Data Models:** Data structures corresponding to the database tables (e.g., `Document`, `Organization`) are defined in `MedRecPro.DataModels` (`LabelClasses.cs`).
* **Database Schema:** The underlying database schema is defined in `MedRecPro-TableCreation.sql`.

## Label CRUD API Endpoints

### Labels (`/api/Labels`)

This controller manages `Label Section` entities, providing CRUD operations based on SPL metadata.

* **`GET /api/Labels`**: Retrieves all Label Section records.
    * **Response:** `200 OK` - Returns a list of documents.
* **`GET /api/Labels/encryptedId`**: Retrieves a specific Label Section record by its primary key (`Object Identifier`).
    * **Parameters:** `encryptedId` (integer, path) - The `Object Identifier` of the label to retrieve.
    * **Responses:**
        * `200 OK` - Returns the requested label.
        * `404 Not Found` - If the label with the specified ID is not found.
* **`POST /api/Labels`**: Creates a new Label Section record.
    * **Request Body:** A `Label Section` object (defined in `LabelClasses.cs`). `Object Identifier` should be omitted or 0 as it is auto-generated.
    * **Responses:**
        * `201 Created` - Returns the newly created label, including its assigned `Object Identifier`.
        * `400 Bad Request` - If the input label data is invalid.
* **`PUT /api/Labels/encryptedId`**: Updates an existing Label Section record.
    * **Parameters:** `encryptedId` (integer, path) - The `Object Identifier` of the label to update.
    * **Request Body:** The updated `Label Section` object. The `Object Identifier` in the body must match the `encryptedId` in the route.
    * **Responses:**
        * `204 No Content` - If the update was successful.
        * `400 Bad Request` - If the ID in the route doesn't match the ID in the body, or if the data is invalid.
        * `404 Not Found` - If the label with the specified ID is not found.
* **`DELETE /api/Labels/encryptedId;`**: Deletes a Label Section record by its ID.
    * **Parameters:** `encryptedId` (integer, path) - The `Object Identifier` of the label to delete.
    * **Responses:**
        * `204 No Content` - If the deletion was successful.
        * `404 Not Found` - If the label with the specified ID is not found.

*(Examples for each endpoint can be found in the XML comments within `LabelController.cs`)*.

---

## Database Caching:
The system implements both timer-based and managed caching for database requests to optimize performance.
Retrieving data from cache is significantly faster than querying the database directly.
When you need to ensure fresh data from the database, use the `/REST/API/Utility/ClearManagedCache` endpoint to clear the managed cache before making your request."
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

app.UseHttpsRedirection();
app.UseRouting();

// Authentication & Authorization must come after UseRouting and before UseEndpoints
app.UseAuthentication(); // Enables authentication capabilities
app.UseAuthorization();  // Enables authorization capabilities

app.MapControllers();

Util.Initialize(httpContextAccessor: app.Services.GetRequiredService<IHttpContextAccessor>());

app.Run();