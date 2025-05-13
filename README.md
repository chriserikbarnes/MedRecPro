# MedRecPro

MedRecPro is a comprehensive medical records management system built with ASP.NET Core, providing secure access to health data through a RESTful API.

## Features

- **User Authentication**: Secure authentication using ASP.NET Identity with support for external providers
- **Document Management**: Full CRUD operations for medical documents based on SPL (Structured Product Labeling) standards
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

### Documents
- `GET /api/documents` - Retrieves all documents
- `GET /api/documents/{id}` - Retrieves a specific document
- `POST /api/documents` - Creates a new document
- `PUT /api/documents/{id}` - Updates an existing document
- `DELETE /api/documents/{id}` - Deletes a document

## Database Schema

The database includes tables for:

- Users and authentication
- Medical documents and metadata
- Organizations and contacts
- Document sections and content
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
  "Security": {
    "DB": {
      "PKSecret": "your-encryption-key-here"
    }
  }
}
```

## License

See the [LICENSE.txt](LICENSE.txt) file for details.