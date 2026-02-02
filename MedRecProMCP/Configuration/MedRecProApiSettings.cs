/**************************************************************/
/// <summary>
/// Configuration settings for the MedRecPro API backend.
/// </summary>
/// <remarks>
/// These settings configure how the MCP server communicates
/// with the existing MedRecPro Web API.
/// </remarks>
/**************************************************************/

namespace MedRecProMCP.Configuration;

/**************************************************************/
/// <summary>
/// MedRecPro API configuration settings.
/// </summary>
/**************************************************************/
public class MedRecProApiSettings
{
    /**************************************************************/
    /// <summary>
    /// Base URL of the MedRecPro API (e.g., https://www.medrecpro.com/api).
    /// </summary>
    /**************************************************************/
    public string BaseUrl { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    /**************************************************************/
    public int TimeoutSeconds { get; set; } = 30;

    /**************************************************************/
    /// <summary>
    /// Whether to validate SSL certificates.
    /// </summary>
    /// <remarks>
    /// Should always be true in production.
    /// May be disabled for local development with self-signed certs.
    /// </remarks>
    /**************************************************************/
    public bool ValidateSslCertificate { get; set; } = true;

    /**************************************************************/
    /// <summary>
    /// Number of retry attempts for failed requests.
    /// </summary>
    /**************************************************************/
    public int RetryCount { get; set; } = 3;

    /**************************************************************/
    /// <summary>
    /// Delay between retry attempts in milliseconds.
    /// </summary>
    /**************************************************************/
    public int RetryDelayMilliseconds { get; set; } = 1000;
}
