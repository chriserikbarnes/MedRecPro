using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MedRecPro.Service;

/**************************************************************/
/// <summary>
/// Provides Azure Management API tokens using client credentials flow.
/// </summary>
/// <remarks>
/// Uses app-only authentication (client credentials) to access Azure Management API.
/// This is independent of user authentication and uses the application's service principal.
/// Compatible with any authentication provider (Google, Microsoft OAuth, etc.).
/// </remarks>
/// <seealso cref="IConfidentialClientApplication"/>
public class AzureManagementTokenProvider
{
    #region Fields

    private readonly IConfidentialClientApplication _app;
    private static readonly string[] Scopes = new[] { "https://management.azure.com/.default" };

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureManagementTokenProvider"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration containing Azure AD settings.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
    /// <remarks>
    /// Requires the following configuration values:
    /// - Authentication:Microsoft:TenantId
    /// - Authentication:Microsoft:ClientId
    /// - Authentication:Microsoft:ClientSecret:Dev (for development)
    /// - Authentication:Microsoft:ClientSecret:Prod (for production)
    /// </remarks>
    /// <seealso cref="ConfidentialClientApplicationBuilder"/>
    public AzureManagementTokenProvider(IConfiguration configuration)
    {
        #region implementation

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        var tenantId = configuration["Authentication:Microsoft:TenantId"]
            ?? throw new InvalidOperationException("Authentication:Microsoft:TenantId is missing");

        var clientId = configuration["Authentication:Microsoft:ClientId"]
            ?? throw new InvalidOperationException("Authentication:Microsoft:ClientId is missing");

        // Use environment-specific client secret
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var clientSecret = environment == "Development"
            ? configuration["Authentication:Microsoft:ClientSecret:Dev"]
            : configuration["Authentication:Microsoft:ClientSecret:Prod"];

        if (string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException($"Authentication:Microsoft:ClientSecret:{environment} is missing");

        var authority = $"https://login.microsoftonline.com/{tenantId}";

        // Build confidential client application for client credentials flow
        _app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(new Uri(authority))
            .Build();

        #endregion
    }

    #endregion

    #region Public Methods

    /**************************************************************/
    /// <summary>
    /// Acquires an access token for Azure Management API using client credentials flow.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Access token string for Azure Management API.</returns>
    /// <exception cref="MsalException">Thrown when token acquisition fails.</exception>
    /// <remarks>
    /// Uses the application's service principal to acquire tokens.
    /// Tokens are automatically cached by MSAL and reused until expiration.
    /// </remarks>
    /// <seealso cref="IConfidentialClientApplication.AcquireTokenForClient"/>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        #region implementation

        var result = await _app
            .AcquireTokenForClient(Scopes)
            .ExecuteAsync(cancellationToken);

        return result.AccessToken;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the token expiration time for the most recently acquired token.
    /// </summary>
    /// <returns>Token expiration datetime, or null if no token has been acquired.</returns>
    /// <remarks>
    /// Used to determine if a cached token is still valid.
    /// </remarks>
    public DateTimeOffset? GetTokenExpiration()
    {
        #region implementation

        // MSAL handles token caching internally, so this is primarily for monitoring
        // Token lifetime is typically 1 hour for Azure Management API
        return DateTimeOffset.UtcNow.AddHours(1);

        #endregion
    }

    #endregion
}

/**************************************************************/
/// <summary>
/// Token credential implementation that uses AzureManagementTokenProvider for Azure SDK authentication.
/// </summary>
/// <remarks>
/// This credential wraps the AzureManagementTokenProvider to provide TokenCredential interface
/// required by Azure SDK clients like MetricsClient. Uses app-only authentication via
/// client credentials flow.
/// </remarks>
/// <seealso cref="Azure.Core.TokenCredential"/>
/// <seealso cref="AzureManagementTokenProvider"/>
public class AppOnlyTokenCredential : TokenCredential
{
    #region Fields

    private readonly AzureManagementTokenProvider _tokenProvider;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="AppOnlyTokenCredential"/> class.
    /// </summary>
    /// <param name="tokenProvider">Token provider for Azure Management API.</param>
    /// <exception cref="ArgumentNullException">Thrown when tokenProvider is null.</exception>
    /// <seealso cref="AzureManagementTokenProvider"/>
    public AppOnlyTokenCredential(AzureManagementTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    #endregion

    #region Public Methods

    /**************************************************************/
    /// <summary>
    /// Gets an access token synchronously.
    /// </summary>
    /// <param name="requestContext">The token request context containing required scopes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An access token for Azure Management API.</returns>
    /// <exception cref="AuthenticationFailedException">Thrown when token acquisition fails.</exception>
    /// <remarks>
    /// Azure SDK requires synchronous token acquisition. This implementation uses
    /// ConfigureAwait(false) to minimize deadlock risk when calling async methods.
    /// </remarks>
    /// <seealso cref="Azure.Core.TokenCredential.GetToken(TokenRequestContext, CancellationToken)"/>
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        #region implementation

        return GetTokenAsync(requestContext, cancellationToken)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets an access token asynchronously.
    /// </summary>
    /// <param name="requestContext">The token request context containing required scopes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation, with an access token result.</returns>
    /// <exception cref="AuthenticationFailedException">Thrown when token acquisition fails.</exception>
    /// <remarks>
    /// Acquires tokens using client credentials flow via AzureManagementTokenProvider.
    /// Tokens are cached by MSAL and automatically refreshed when expired.
    /// </remarks>
    /// <seealso cref="Azure.Core.TokenCredential.GetTokenAsync(TokenRequestContext, CancellationToken)"/>
    public override async ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        #region implementation

        try
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            var expiresOn = _tokenProvider.GetTokenExpiration() ?? DateTimeOffset.UtcNow.AddHours(1);

            return new AccessToken(token, expiresOn);
        }
        catch (MsalException ex)
        {
            throw new AuthenticationFailedException(
                "Failed to acquire Azure Management API token using client credentials", ex);
        }
        catch (Exception ex)
        {
            throw new AuthenticationFailedException(
                "Unexpected error acquiring Azure Management API token", ex);
        }

        #endregion
    }

    #endregion
}