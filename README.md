# MedRecPro

MedRecPro is a structured product label management system built with ASP.NET Core, providing secure access to data through a RESTful API.

## Specifications

- **HL7 Version**: HL7 Dec 2023 https://www.fda.gov/media/84201/download
- **Info**: https://www.fda.gov/industry/fda-data-standards-advisory-board/structured-product-labeling-resources
- **Data Source**: https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm

## Features

- **User Authentication**: Secure authentication using ASP.NET Identity with support for external providers
- **Document Management**: Full CRUD operations for medical labels based on SPL (Structured Product Labeling) standards
- **Data Security**: Encrypted user identifiers and sensitive information
- **Role-based Access Control**: Granular permissions system for different user roles
- **API Documentation**: Swagger/OpenAPI integration

## Technology Stack

- **Backend**: ASP.NET Core (.NET 6+)
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: Cookie-based authentication with external provider support
- **API Documentation**: Swagger/OpenAPI
- **Security**: 
  - Identity framework for authentication
  - Encryption mechanisms for sensitive data
  - Bearer token authentication for API endpoints

## API Endpoints

### Authentication
- `GET /api/auth/login/{provider}` - Initiates login with external provider
- `GET /api/auth/external-logincallback` - Callback for external authentication
- `GET /api/auth/user` - Retrieves current user info
- `POST /api/auth/logout` - Logs out the current user

### Users
- `GET /api/users/GetUser/{encryptedUserId}` - Retrieves user information
- `POST /api/users/CreateUser` - Creates a new user

### Labels
- `GET /api/label` - Retrieves all labels
- `GET /api/label/{encryptedId}` - Retrieves a specific item
- `POST /api/label` - Creates a new item
- `PUT /api/label/{encryptedId}` - Updates an existing item
- `DELETE /api/label/{encryptedId}` - Deletes a item

## Database Schema

The database includes tables for:

- Users and authentication
- SPL labels and metadata
- Organizations and contacts
- Relationships between entities

## Getting Started

1. Clone the repository
2. Configure your database connection in `appsettings.json`
3. Run database migrations:
   ```
   dotnet ef database update
   ```
4. Run the application:
   ```
   dotnet run
   ```
5. Access the API at `https://localhost:5001` or as configured

## Security Configuration

Security settings including encryption keys should be configured in your user secrets or environment variables:

```json
{
    "Authentication:Google:ClientSecret" : "your-google-client-secret-here",
    "Authentication:Google:ClientId" : "your-google-client-id-here",
    "Security:DB:PKSecret": "your-encryption-key-here",
    "Dev:DB:Connection": "Server=localhost;Database=MedRecPro;User Id=sa;Password=your_password_here;",
    "Jwt":{
        "Key": "your-super-strong-key",
        "Issuer": "MedRecPro",
        "Audience": "MedRecUsers",
        "ExpirationMinutes": 60
    }
}
```

## License

See the [LICENSE.txt](LICENSE.txt) file for details.
