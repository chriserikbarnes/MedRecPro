
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MedRecPro.Helpers; // Namespace for StringCipher, AppSettings etc.
using Microsoft.AspNetCore.Authentication.Cookies;
using MedRecPro.Data; // Namespace for ApplicationDbContext
using Microsoft.AspNetCore.Identity;
using MedRecPro.Models; // Namespace for User model
using System.Security.Cryptography;
using MedRecPro.DataAccess;
using MedRecPro.Security; // Namespace for BasicAuthenticationHandler
using Microsoft.AspNetCore.Authentication; // Required for AuthenticationBuilder
using Microsoft.AspNet.Identity;
using System.Reflection;


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

builder.Services.AddUserLogger(); // Assuming this is your custom service

builder.Services.AddTransient<StringCipher>();

builder.Services.AddEndpointsApiExplorer();

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


// Register our custom IPasswordHasher for MedRecPro.Models.User.
// AddIdentity<User,...> already registers IPasswordHasher<User>.
// This line is only needed if your PasswordHasher<User> is a *different* implementation
// than the one Identity would register by default for your User type.
// If it's just using the standard Identity password hashing algorithm, this is redundant.
// Assuming MedRecPro.Models.PasswordHasher<User> is your specific implementation.
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();


// --- Custom Services ---
builder.Services.AddScoped<UserDataAccess>();


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
builder.Services.AddSwaggerGen(options =>
{
#if DEBUG || DEV
    var environment = "Dev";
    var serverName = "Localhost";

#elif RELEASE
    var environment = "Prod";
    var serverName = "ProdHost";
#endif

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

    options.AddSecurityDefinition("BasicAuthentication", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Basic",
        In = ParameterLocation.Header,
        Description = "Basic Authorization header using the Basic scheme. Example: \"Authorization: Basic {base64(email:password)}\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) // Check if the file exists before trying to include it
    {
        options.IncludeXmlComments(xmlPath);
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

app.Run();