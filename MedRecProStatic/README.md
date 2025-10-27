# MedRecPro.Static

Static marketing and documentation site for MedRecPro - Pharmaceutical Labeling Management System.

## Overview

This project provides the public-facing website for MedRecPro, including:
- Home page with product information and features
- Terms of Service
- Privacy Policy
- Navigation to API authentication and documentation

The site is built with ASP.NET Core Razor Pages and uses a simple JSON-based content management system.

## Project Structure

```
MedRecPro.Static/
│
├── Controllers/              ← Contains HomeController.cs
│   └── HomeController.cs    ← Single controller with 3 actions
│
├── Views/                    ← Contains all views
│   ├── Home/                ← Views for HomeController
│   │   ├── Index.cshtml     ← Home page
│   │   ├── Terms.cshtml     ← Terms page
│   │   └── Privacy.cshtml   ← Privacy page
│   └── Shared/              ← Shared layouts
│       ├── _Layout.cshtml   ← Main layout
│       └── _ViewImports.cshtml
│
├── Models/                   ← Data models
├── Services/                 ← Business logic
├── Content/                  ← JSON content files
└── wwwroot/                  ← Static assets
```

## Local Development

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code

### Running Locally

```bash
cd MedRecPro.Static
dotnet restore
dotnet run
```

The site will be available at `https://localhost:5001`

### Editing Content

All page content is managed via JSON files in the `Content/` directory:

**config.json** - Site-wide settings:
```json
{
  "siteName": "MedRecPro",
  "tagline": "Pharmaceutical Labeling Management System",
  "apiUrl": "https://medrecpro.com/api",
  "contactEmail": "support@medrecpro.com"
}
```

**pages.json** - Page content:
```json
{
  "home": { ... },
  "terms": { ... },
  "privacy": { ... }
}
```

After editing JSON files, rebuild and run the project to see changes.

## Deployment

This project is deployed to Azure App Service alongside the MedRecPro API:

- **Static Site**: `https://medrecpro.com/` (root path)
- **API**: `https://medrecpro.com/api`

### Manual Deployment

```bash
dotnet publish -c Release -o ./publish
# Deploy to Azure App Service
```

### Automated Deployment

GitHub Actions workflow automatically deploys on push to `main` branch.

## Architecture

### Content Management
- **JSON-based**: Easy to edit, version control friendly
- **Type-safe**: Strongly-typed models with PageContent.cs
- **Singleton Service**: ContentService loads JSON once at startup

### Razor Pages
- **Index**: Home page with hero section and features
- **Terms**: Terms of Service (required for Azure App Registration)
- **Privacy**: Privacy Policy (required for Azure App Registration)
- **Shared/_Layout**: Common layout with navigation and footer

### Styling
- Responsive design using CSS Grid and Flexbox
- Mobile-first approach
- Modern, clean aesthetic

## URL Structure

| Page     | URL               | Description               |
|------- --|-------------------|------------------------- -|
| Home     | `/`               | Marketing home page       |
| Terms    | `/terms`          | Terms of Service          |
| Privacy  | `/privacy`        | Privacy Policy            |
| API Docs | `/api/swagger`    | Swagger API documentation |
| Auth     | `/api/auth/login` | Authentication endpoint   |

## Features

- ✅ Clean, modern design
- ✅ Responsive mobile layout
- ✅ JSON-based content management
- ✅ Easy to maintain and update
- ✅ No database required
- ✅ Fast load times
- ✅ SEO-friendly structure

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Program.cs

Key services registered:
- Razor Pages
- Static Files Middleware
- ContentService (Singleton)

## Azure App Service Configuration

### Virtual Applications

The static site runs at the root path (`/`) while the API runs at `/api`:

| Virtual Path | Physical Path | Application |
|--------------|---------------|-------------|
| `/` | `site\wwwroot\Static` | No |
| `/api` | `site\wwwroot\Api` | Yes |

### Custom Domain

- Primary: `https://medrecpro.com`
- SSL/TLS: Azure-managed certificate
- CDN: Cloudflare (optional)

## Content Guidelines

### Updating Terms or Privacy

1. Edit `Content/pages.json`
2. Update the `lastUpdated` field
3. Modify sections as needed
4. Commit changes to Git
5. Deploy to production

### Adding New Features

To add a new feature to the home page:

```json
{
  "home": {
    "features": [
      {
        "icon": "🆕",
        "title": "New Feature",
        "description": "Description of your new feature"
      }
    ]
  }
}
```

### Updating Configuration

Edit `Content/config.json`:
```json
{
  "contactEmail": "newemail@medrecpro.com",
  "apiUrl": "https://newdomain.com/api"
}
```

## License

This project is part of MedRecPro and follows the same license (Apache 2.0).

## Support

For questions or issues:
- **Email**: support@medrecpro.com
- **GitHub Issues**: [MedRecPro Issues](https://github.com/yourusername/medrecpro/issues)
- **Documentation**: See main MedRecPro repository

## Related Projects

- **MedRecPro (API)**: Main application with SPL processing and authentication
- **MedRecPro.Tests**: Unit test project

## Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

Content changes (JSON files) are especially welcome.

---

**Version**: 1.0.0-alpha 
**Last Updated**: October 27, 2025  
**Status**: alpa-ready
