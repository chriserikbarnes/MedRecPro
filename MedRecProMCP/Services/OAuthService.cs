/**************************************************************/
/// <summary>
/// Implementation of OAuth service for upstream provider interactions.
/// </summary>
/// <remarks>
/// This service handles OAuth flows with Google and Microsoft identity
/// providers. It manages:
/// - Authorization URL generation
/// - Authorization code exchange for tokens
/// - Token refresh operations
/// - User information retrieval
///
/// Configuration paths (matching secrets.json):
/// - Authentication:Google:ClientId, Authentication:Google:ClientSecret
/// - Authentication:Microsoft:ClientId, Authentication:Microsoft:ClientSecret:Dev/Prod
/// - Authentication:Microsoft:TenantId
/// </remarks>
/// <seealso cref="IOAuthService"/>
/// <seealso cref="AuthenticationSettings"/>
/**************************************************************/

using MedRecProMCP.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service for OAuth interactions with upstream identity providers.
/// </summary>
/**************************************************************/
public class OAuthService : IOAuthService
{
    private readonly AuthenticationSettings _authSettings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OAuthService> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "google",
        "microsoft"
    };

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of OAuthService.
    /// </summary>
    /// <remarks>
    /// Injects AuthenticationSettings which maps to the Authentication section
    /// in configuration (matching secrets.json key structure).
    /// </remarks>
    /**************************************************************/
    public OAuthService(
        IOptions<AuthenticationSettings> authSettings,
        IHttpClientFactory httpClientFactory,
        ILogger<OAuthService> logger,
        IHostEnvironment environment)
    {
        _authSettings = authSettings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _environment = environment;
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public string GetAuthorizationUrl(
        string provider,
        string state,
        string codeChallenge,
        string redirectUri,
        IEnumerable<string>? scopes = null)
    {
        #region implementation
        if (!IsProviderSupported(provider))
        {
            throw new ArgumentException($"Unsupported provider: {provider}", nameof(provider));
        }

        var queryParams = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["redirect_uri"] = redirectUri,
            ["access_type"] = "offline", // Request refresh token
            ["prompt"] = "consent" // Force consent to get refresh token
        };

        string baseUrl;
        string clientId;
        string[] defaultScopes;

        if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = _authSettings.Google.AuthorizationEndpoint;
            clientId = _authSettings.Google.ClientId;
            defaultScopes = _authSettings.Google.Scopes;
        }
        else // microsoft
        {
            baseUrl = _authSettings.Microsoft.AuthorizationEndpoint;
            clientId = _authSettings.Microsoft.ClientId;
            defaultScopes = _authSettings.Microsoft.Scopes;
        }

        queryParams["client_id"] = clientId;
        queryParams["scope"] = string.Join(" ", scopes ?? defaultScopes);

        var queryString = string.Join("&",
            queryParams.Select(kvp =>
                $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        var authUrl = $"{baseUrl}?{queryString}";

        _logger.LogDebug(
            "[OAuth] Generated authorization URL for provider {Provider}",
            provider);

        return authUrl;
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<UpstreamTokenResult?> ExchangeCodeForTokensAsync(
        string provider,
        string code,
        string codeVerifier,
        string redirectUri)
    {
        #region implementation
        if (!IsProviderSupported(provider))
        {
            throw new ArgumentException($"Unsupported provider: {provider}", nameof(provider));
        }

        string tokenEndpoint;
        string clientId;
        string clientSecret;

        if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
        {
            tokenEndpoint = _authSettings.Google.TokenEndpoint;
            clientId = _authSettings.Google.ClientId;
            clientSecret = _authSettings.Google.ClientSecret;
        }
        else // microsoft
        {
            tokenEndpoint = _authSettings.Microsoft.TokenEndpoint;
            clientId = _authSettings.Microsoft.ClientId;
            // Use environment-appropriate client secret (Dev vs Prod)
            clientSecret = getMicrosoftClientSecret();
        }

        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code_verifier"] = codeVerifier
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(tokenRequest));

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "[OAuth] Token exchange failed for {Provider}: {StatusCode} - {Response}",
                    provider, response.StatusCode, responseContent);
                return null;
            }

            var tokenResponse = JsonSerializer.Deserialize<UpstreamTokenResponse>(responseContent);
            if (tokenResponse == null)
            {
                _logger.LogError("[OAuth] Failed to deserialize token response");
                return null;
            }

            var result = new UpstreamTokenResult
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresIn = tokenResponse.ExpiresIn,
                IdToken = tokenResponse.IdToken,
                TokenType = tokenResponse.TokenType ?? "Bearer",
                Scope = tokenResponse.Scope
            };

            // Get user info
            result.UserInfo = await GetUserInfoAsync(provider, result.AccessToken);

            _logger.LogInformation(
                "[OAuth] Successfully exchanged code for tokens from {Provider}",
                provider);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OAuth] Token exchange error for {Provider}", provider);
            return null;
        }
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<UpstreamTokenResult?> RefreshUpstreamTokenAsync(
        string provider,
        string refreshToken)
    {
        #region implementation
        if (!IsProviderSupported(provider))
        {
            throw new ArgumentException($"Unsupported provider: {provider}", nameof(provider));
        }

        string tokenEndpoint;
        string clientId;
        string clientSecret;

        if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
        {
            tokenEndpoint = _authSettings.Google.TokenEndpoint;
            clientId = _authSettings.Google.ClientId;
            clientSecret = _authSettings.Google.ClientSecret;
        }
        else // microsoft
        {
            tokenEndpoint = _authSettings.Microsoft.TokenEndpoint;
            clientId = _authSettings.Microsoft.ClientId;
            // Use environment-appropriate client secret (Dev vs Prod)
            clientSecret = getMicrosoftClientSecret();
        }

        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(tokenRequest));

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "[OAuth] Token refresh failed for {Provider}: {StatusCode} - {Response}",
                    provider, response.StatusCode, responseContent);
                return null;
            }

            var tokenResponse = JsonSerializer.Deserialize<UpstreamTokenResponse>(responseContent);
            if (tokenResponse == null)
            {
                _logger.LogError("[OAuth] Failed to deserialize refresh token response");
                return null;
            }

            _logger.LogInformation(
                "[OAuth] Successfully refreshed token from {Provider}",
                provider);

            return new UpstreamTokenResult
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken ?? refreshToken, // Some providers don't return new refresh token
                ExpiresIn = tokenResponse.ExpiresIn,
                IdToken = tokenResponse.IdToken,
                TokenType = tokenResponse.TokenType ?? "Bearer",
                Scope = tokenResponse.Scope
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OAuth] Token refresh error for {Provider}", provider);
            return null;
        }
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<UpstreamUserInfo?> GetUserInfoAsync(string provider, string accessToken)
    {
        #region implementation
        if (!IsProviderSupported(provider))
        {
            throw new ArgumentException($"Unsupported provider: {provider}", nameof(provider));
        }

        string userInfoEndpoint;

        if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
        {
            userInfoEndpoint = _authSettings.Google.UserInfoEndpoint;
        }
        else // microsoft
        {
            userInfoEndpoint = _authSettings.Microsoft.UserInfoEndpoint;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync(userInfoEndpoint);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "[OAuth] User info request failed for {Provider}: {StatusCode}",
                    provider, response.StatusCode);
                return null;
            }

            UpstreamUserInfo userInfo;

            if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
            {
                var googleUser = JsonSerializer.Deserialize<GoogleUserInfo>(responseContent);
                if (googleUser == null) return null;

                userInfo = new UpstreamUserInfo
                {
                    Id = googleUser.Sub,
                    Email = googleUser.Email,
                    EmailVerified = googleUser.EmailVerified,
                    Name = googleUser.Name,
                    GivenName = googleUser.GivenName,
                    FamilyName = googleUser.FamilyName,
                    Picture = googleUser.Picture,
                    Locale = googleUser.Locale,
                    Provider = "google"
                };
            }
            else // microsoft
            {
                var msUser = JsonSerializer.Deserialize<MicrosoftUserInfo>(responseContent);
                if (msUser == null) return null;

                userInfo = new UpstreamUserInfo
                {
                    Id = msUser.Id,
                    Email = msUser.Mail ?? msUser.UserPrincipalName,
                    EmailVerified = true, // Microsoft Graph doesn't provide this directly
                    Name = msUser.DisplayName,
                    GivenName = msUser.GivenName,
                    FamilyName = msUser.Surname,
                    Provider = "microsoft"
                };
            }

            _logger.LogDebug(
                "[OAuth] Retrieved user info for {Provider}: {Email}",
                provider, userInfo.Email);

            return userInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OAuth] User info request error for {Provider}", provider);
            return null;
        }
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public bool IsProviderSupported(string provider)
    {
        return SupportedProviders.Contains(provider);
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public IEnumerable<string> GetSupportedProviders()
    {
        return SupportedProviders;
    }

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Gets the appropriate Microsoft client secret based on the current environment.
    /// </summary>
    /// <remarks>
    /// Uses Authentication:Microsoft:ClientSecret:Dev for development
    /// and Authentication:Microsoft:ClientSecret:Prod for production.
    /// </remarks>
    /**************************************************************/
    private string getMicrosoftClientSecret()
    {
        return _environment.IsDevelopment()
            ? _authSettings.Microsoft.ClientSecret.Dev
            : _authSettings.Microsoft.ClientSecret.Prod;
    }

    #endregion
}

#region Internal DTOs

/**************************************************************/
/// <summary>
/// OAuth token response from upstream providers.
/// </summary>
/**************************************************************/
internal class UpstreamTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }
}

/**************************************************************/
/// <summary>
/// Google user info response.
/// </summary>
/**************************************************************/
internal class GoogleUserInfo
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_verified")]
    public bool EmailVerified { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; set; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; set; }

    [JsonPropertyName("picture")]
    public string? Picture { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }
}

/**************************************************************/
/// <summary>
/// Microsoft Graph user info response.
/// </summary>
/**************************************************************/
internal class MicrosoftUserInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("givenName")]
    public string? GivenName { get; set; }

    [JsonPropertyName("surname")]
    public string? Surname { get; set; }

    [JsonPropertyName("mail")]
    public string? Mail { get; set; }

    [JsonPropertyName("userPrincipalName")]
    public string? UserPrincipalName { get; set; }

    [JsonPropertyName("jobTitle")]
    public string? JobTitle { get; set; }

    [JsonPropertyName("officeLocation")]
    public string? OfficeLocation { get; set; }
}

#endregion
