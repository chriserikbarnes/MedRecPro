using MedRecPro.Data;
using MedRecPro.Models;
using MedRecPro.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace MedRecPro.Configuration
{
    /**************************************************************/
    /// <summary>
    /// Provides composition-root extension methods for MedRecPro session, identity, authentication, CORS, and authorization setup.
    /// </summary>
    /// <remarks>
    /// Authentication provider configuration is kept separate from general service registration so secret lookup and scheme ordering stay reviewable.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMedRecProSession();
    /// services.AddMedRecProAuth(configuration);
    /// </code>
    /// </example>
    /// <seealso cref="AuthenticationBuilder"/>
    /// <seealso cref="AuthorizationPolicyBuilder"/>
    public static class MedRecProAuthenticationExtensions
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Registers MedRecPro session configuration and cookie settings.
        /// </summary>
        /// <remarks>
        /// The session cookie name, idle timeout, and essential/HTTP-only flags match the previous composition root values.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProSession();
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="SessionOptions"/>
        public static IServiceCollection AddMedRecProSession(this IServiceCollection services)
        {
            #region implementation

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.Name = ".MedRecPro.Session";
            });

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers MedRecPro identity, cookie, CORS, authentication-provider, MCP JWT, password-hasher, and authorization policy setup.
        /// </summary>
        /// <remarks>
        /// The registration order preserves the original Identity then authentication scheme sequence, including the later MCP bearer add-on.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProAuth(configuration);
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Configuration containing provider IDs, secrets, and MCP token settings.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="IdentityBuilder"/>
        /// <seealso cref="BasicAuthenticationHandler"/>
        /// <seealso cref="JwtBearerHandler"/>
        public static IServiceCollection AddMedRecProAuth(this IServiceCollection services, IConfiguration configuration)
        {
            #region implementation

            addMedRecProIdentity(services);
            configureMedRecProApplicationCookie(services);
            addMedRecProCors(services);
            addMedRecProAuthenticationProviders(services, configuration);
            addMedRecProMcpJwtAuthentication(services, configuration);

            // Register custom IPasswordHasher for MedRecPro.Models.User.
            // AddIdentity<User,...> already registers IPasswordHasher<User>.
            services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

            addMedRecProAuthorization(services);

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers ASP.NET Core Identity for MedRecPro users and roles.
        /// </summary>
        /// <remarks>
        /// Identity registration calls <c>AddAuthentication()</c> internally and sets up the Identity cookie schemes.
        /// </remarks>
        /// <param name="services">The service collection to configure.</param>
        /// <seealso cref="User"/>
        /// <seealso cref="IdentityRole{TKey}"/>
        private static void addMedRecProIdentity(IServiceCollection services)
        {
            #region implementation

            // --- ASP.NET Core Identity ---
            // This registers UserManager, SignInManager, RoleManager, IPasswordHasher,
            // and also calls services.AddAuthentication().AddIdentityCookies() internally,
            // which sets up schemes like IdentityConstants.ApplicationScheme.
            services.AddIdentity<User, IdentityRole<long>>(options =>
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

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Configures the application cookie behavior used by the Identity application scheme.
        /// </summary>
        /// <remarks>
        /// The event handlers intentionally preserve the existing host-based cookie-domain selection and logging behavior.
        /// </remarks>
        /// <param name="services">The service collection to configure.</param>
        /// <seealso cref="CookieAuthenticationEvents"/>
        private static void configureMedRecProApplicationCookie(IServiceCollection services)
        {
            #region implementation

            // --- Authentication Configuration ---
            // AddIdentity has already called AddAuthentication() and added cookie schemes.
            services.ConfigureApplicationCookie(options =>
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
                    OnRedirectToLogin = ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    },
                    OnSigningIn = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("CookieAuthentication");

                        var host = ctx.HttpContext.Request.Host.Host.ToLowerInvariant();
                        var userName = ctx.Principal?.Identity?.Name ?? "unknown";
                        string? cookieDomain = null;

                        if (host.EndsWith("medrecpro.com"))
                        {
                            cookieDomain = ".medrecpro.com";
                        }
                        else if (host.EndsWith("medrec.pro"))
                        {
                            cookieDomain = ".medrec.pro";
                        }
                        // localhost / 127.0.0.1 / local IPs: don't set domain

                        if (cookieDomain != null)
                        {
                            ctx.Options.Cookie.Domain = cookieDomain;
                            logger.LogInformation(
                                "[Auth] Signing in user '{User}' on host '{Host}' - Cookie domain set to '{Domain}'",
                                userName, host, cookieDomain);
                        }
                        else
                        {
                            logger.LogInformation(
                                "[Auth] Signing in user '{User}' on host '{Host}' - Cookie domain not set (localhost mode)",
                                userName, host);
                        }

                        return Task.CompletedTask;
                    },
                    OnSignedIn = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("CookieAuthentication");

                        var userName = ctx.Principal?.Identity?.Name ?? "unknown";
                        logger.LogInformation(
                            "[Auth] Successfully signed in user '{User}' - Cookie path: '{Path}', Domain: '{Domain}'",
                            userName,
                            ctx.Options.Cookie.Path,
                            ctx.Options.Cookie.Domain ?? "(not set)");

                        return Task.CompletedTask;
                    },
                    OnValidatePrincipal = ctx =>
                    {
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("CookieAuthentication");

                        var userName = ctx.Principal?.Identity?.Name ?? "unknown";
                        var host = ctx.HttpContext.Request.Host.Host;

                        logger.LogDebug(
                            "[Auth] Validating cookie for user '{User}' on host '{Host}'",
                            userName, host);

                        return Task.CompletedTask;
                    }
                };
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds local-development and production CORS policies.
        /// </summary>
        /// <remarks>
        /// The policy names and exposed pagination headers are part of the existing browser/API contract.
        /// </remarks>
        /// <param name="services">The service collection to configure.</param>
        /// <seealso cref="CorsOptions"/>
        private static void addMedRecProCors(IServiceCollection services)
        {
            #region implementation

            // Add CORS for local testing if needed.
            services.AddCors(options =>
            {
                #region implementation

                /**************************************************************/
                // AllowLocalDevelopment permits cross-origin requests from local development origins:
                // localhost, 127.0.0.1, local network IPs, and common Kestrel/Node/HTTP ports.
                /**************************************************************/
                options.AddPolicy("AllowLocalDevelopment", policy =>
                {
                    policy.WithOrigins(
                        // Localhost HTTP variants
                        "http://localhost",
                        "http://localhost:5000",
                        "http://localhost:5001",
                        "http://localhost:5173",   // Vite default
                        "http://localhost:50346",  // MedRecPro AE dashboard Vite dev server
                        "http://localhost:3000",   // React/Node default
                        "http://localhost:8080",   // Common alternative

                        // Localhost HTTPS variants
                        "https://localhost",
                        "https://localhost:5001",
                        "https://localhost:7000",
                        "https://localhost:7001",
                        "https://localhost:7100",
                        "https://localhost:7200",

                        // 127.0.0.1 variants
                        "http://127.0.0.1",
                        "http://127.0.0.1:5000",
                        "http://127.0.0.1:5001",
                        "https://127.0.0.1:5001",
                        "https://127.0.0.1:7000",
                        "https://127.0.0.1:7001"
                    )
                    .AllowAnyMethod()        // Allow GET, POST, PUT, DELETE, etc.
                    .AllowAnyHeader()        // Allow Content-Type, Authorization, etc.
                    .WithExposedHeaders(
                        "X-Page-Number",
                        "X-Page-Size",
                        "X-Total-Count",
                        "X-Chartable-Count")
                    .AllowCredentials();     // Allow cookies/auth headers if needed
                });

                /**************************************************************/
                // Production policy for same-origin and trusted domains.
                // MCP server is hosted at /mcp under www.medrecpro.com (same origin).
                /**************************************************************/
                options.AddPolicy("Production", policy =>
                {
                    policy.WithOrigins(
                        "https://www.medrecpro.com",
                        "https://medrecpro.com"
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders(
                        "X-Page-Number",
                        "X-Page-Size",
                        "X-Total-Count",
                        "X-Chartable-Count")
                    .AllowCredentials();
                });

                #endregion
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers Basic, Google, and Microsoft authentication providers.
        /// </summary>
        /// <remarks>
        /// The Microsoft client-secret branch remains compile-time conditional to preserve Debug versus production behavior.
        /// </remarks>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Configuration containing provider settings.</param>
        /// <seealso cref="BasicAuthenticationHandler"/>
        /// <seealso cref="GoogleOptions"/>
        /// <seealso cref="MicrosoftAccountOptions"/>
        private static void addMedRecProAuthenticationProviders(IServiceCollection services, IConfiguration configuration)
        {
            #region implementation

            string? googleClientId = configuration["Authentication:Google:ClientId"];
            string? googleClientSecret = configuration["Authentication:Google:ClientSecret"];
            string? microsoftClientId = configuration["Authentication:Microsoft:ClientId"];
            string? microsoftClientSecret = null;

#if DEBUG
            microsoftClientSecret = configuration["Authentication:Microsoft:ClientSecret:Dev"];
#else
            microsoftClientSecret = configuration["Authentication:Microsoft:ClientSecret:Prod"];
#endif

            // Authentication schemes for Google, Microsoft and BasicAuthentication.
            services.AddAuthentication(options =>
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
            })
            .AddMicrosoftAccount(options =>
            {
                if (string.IsNullOrWhiteSpace(microsoftClientId) || string.IsNullOrWhiteSpace(microsoftClientSecret))
                {
                    Console.WriteLine("Microsoft ClientId or ClientSecret not configured. Microsoft authentication will be disabled.");
                    return;
                }
                options.ClientId = microsoftClientId;
                options.ClientSecret = microsoftClientSecret;

                // Clear default scopes and add required ones
                options.Scope.Clear();
                options.Scope.Add("openid");      // REQUIRED by Microsoft
                options.Scope.Add("profile");      // For basic profile info
                options.Scope.Add("email");        // For email address
                options.Scope.Add("https://graph.microsoft.com/User.Read");

                options.SaveTokens = true;

                // Optional: Map claims to Identity claims for consistency
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "mail");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "displayName");
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers JWT bearer authentication for tokens issued by the MCP Server when configured.
        /// </summary>
        /// <remarks>
        /// Missing MCP configuration keeps the prior startup behavior: bearer auth is skipped and a console diagnostic is written.
        /// </remarks>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Configuration containing MCP issuer and signing key settings.</param>
        /// <seealso cref="JwtBearerEvents"/>
        private static void addMedRecProMcpJwtAuthentication(IServiceCollection services, IConfiguration configuration)
        {
            #region implementation

            /**************************************************************/
            // Configures JWT Bearer authentication for tokens issued by the MCP Server.
            // This allows MedRecPro API requests through the MCP gateway while preserving user identity.
            /**************************************************************/

            // Get MCP server settings from configuration
            var mcpServerUrl = configuration["McpServer:Url"];
            var mcpJwtSigningKey = configuration["McpServer:JwtSigningKey"];

            if (!string.IsNullOrEmpty(mcpServerUrl) && !string.IsNullOrEmpty(mcpJwtSigningKey))
            {
                #region implementation
                services.AddAuthentication()
                    .AddJwtBearer("McpBearer", options =>
                    {
                        // Disable Microsoft's default claim type mapping so JWT claims
                        // retain their original short names (sub, name, email).
                        // Combined with McpTokenService's claim normalization, this
                        // ensures the ClaimsPrincipal contains predictable claim types.
                        options.MapInboundClaims = false;

                        // Configure token validation parameters for MCP-issued JWTs
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = mcpServerUrl.TrimEnd('/'),
                            ValidAudience = mcpServerUrl.TrimEnd('/'),
                            IssuerSigningKey = new SymmetricSecurityKey(
                                System.Text.Encoding.UTF8.GetBytes(mcpJwtSigningKey)),
                            ClockSkew = TimeSpan.FromMinutes(1),
                            NameClaimType = "name",
                            RoleClaimType = "roles"
                        };

                        // Configure JWT bearer events for logging and diagnostics
                        options.Events = new JwtBearerEvents
                        {
                            OnAuthenticationFailed = context =>
                            {
                                var logger = context.HttpContext.RequestServices
                                    .GetRequiredService<ILoggerFactory>()
                                    .CreateLogger("McpJwtAuth");

                                if (context.Exception is SecurityTokenException)
                                {
                                    logger.LogWarning(
                                        "[MCP Auth] Token validation failed: {Error}",
                                        context.Exception.Message);
                                }
                                else
                                {
                                    logger.LogDebug(
                                        "[MCP Auth] Authentication error: {Error}",
                                        context.Exception.Message);
                                }
                                return Task.CompletedTask;
                            },
                            OnTokenValidated = context =>
                            {
                                var logger = context.HttpContext.RequestServices
                                    .GetRequiredService<ILoggerFactory>()
                                    .CreateLogger("McpJwtAuth");

                                var userName = context.Principal?.Identity?.Name ?? "unknown";
                                var provider = context.Principal?.FindFirst("provider")?.Value ?? "unknown";

                                logger.LogInformation(
                                    "[MCP Auth] Token validated for user: {User} (provider: {Provider})",
                                    userName, provider);

                                return Task.CompletedTask;
                            }
                        };
                    });

                Console.WriteLine($"[Startup] MCP JWT authentication enabled for issuer: {mcpServerUrl}");
                #endregion
            }
            else
            {
                Console.WriteLine("[Startup] MCP JWT authentication not configured (McpServer:Url or McpServer:JwtSigningKey missing)");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers MedRecPro authorization policies and the default API access policy.
        /// </summary>
        /// <remarks>
        /// The default policy accepts both Identity cookies and MCP bearer tokens, preserving the existing mixed browser/MCP gateway behavior.
        /// </remarks>
        /// <param name="services">The service collection to configure.</param>
        /// <seealso cref="AuthorizationPolicy"/>
        private static void addMedRecProAuthorization(IServiceCollection services)
        {
            #region implementation

            // --- Authorization ---
            services.AddAuthorization(options =>
            {
                /**************************************************************/
                // BasicAuthPolicy requires the Basic Authentication scheme.
                /**************************************************************/
                options.AddPolicy("BasicAuthPolicy", new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes("BasicAuthentication") // Ensure this matches the scheme name above
                    .RequireAuthenticatedUser()
                    .Build());

                /**************************************************************/
                // ApiAccess accepts both Identity cookies for browser sessions and MCP JWT bearer tokens.
                /**************************************************************/
                options.AddPolicy("ApiAccess", new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(
                        IdentityConstants.ApplicationScheme,
                        "McpBearer")
                    .RequireAuthenticatedUser()
                    .Build());

                // Make ApiAccess the default policy for API controllers
                // This allows endpoints to accept either cookie auth or MCP JWT tokens
                options.DefaultPolicy = options.GetPolicy("ApiAccess")
                    ?? new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
            });

            #endregion
        }

        #endregion
    }
}
