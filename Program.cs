using System.Reflection;  // Import System.Reflection to enable accessing metadata about assemblies.
using Microsoft.OpenApi.Models;  // Import OpenAPI models for Swagger/OpenAPI configuration.


using Microsoft.AspNetCore.Mvc;  // For IUrlHelper and IUrlHelperFactory
using Microsoft.AspNetCore.Mvc.Infrastructure;  // For IActionContextAccessor
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;  // For service registration extensions
using MedRecPro.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Access the connection string
var connectionString = builder.Configuration.GetSection("Dev:DB:Connection");
Console.WriteLine($"Connection String: {connectionString}");

// Bind AppSettings from configuration to services
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("appSettings"));  // Bind appSettings section to AppSettings class

// Register AppSettings as a transient service in the dependency injection container
builder.Services.AddTransient<AppSettings>();

// Register IHttpContextAccessor for accessing the HTTP context in services
builder.Services.AddHttpContextAccessor();

// Logging Configuration
builder.Services.AddUserLogger();  // Add custom user logging service

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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
});

builder.Services.AddSwaggerGen();

var app = builder.Build();


app.UseRouting();
app.UseHttpsRedirection();
app.UseAuthorization();


// Configure Swagger middleware for API documentation and OAuth2 authentication
app.UseSwagger();  // Enable Swagger middleware
app.UseSwaggerUI();

app.MapControllers();
app.Run();

