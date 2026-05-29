/**************************************************************/
/**
 * Error type used by the AE dashboard API client.
 *
 * The instance keeps the HTTP status code next to the readable message so
 * components can distinguish validation, auth, disabled-feature, and retryable
 * failures without duplicating response parsing.
 */
export class ApiError extends Error {
  /**************************************************************/
  /**
   * Creates a new API error.
   *
   * @param {string} message - Human-readable error message.
   * @param {Response | null} response - Fetch response that caused the failure.
   * @param {unknown} details - Optional parsed response body.
   */
  constructor(message, response = null, details = null) {
    super(message);

    // Preserve the custom name for console diagnostics and instanceof checks.
    this.name = 'ApiError';

    // Preserve the raw response for advanced diagnostics.
    this.response = response;

    // Flatten the status for component-level control flow.
    this.status = response?.status ?? 0;

    // Keep the parsed response body when the server returned useful details.
    this.details = details;
  }

  /**************************************************************/
  /**
   * Indicates that the caller must sign in or obtain API permission.
   *
   * @returns {boolean} True when the status is 401 or 403.
   */
  get isAuthenticationOrPermissionFailure() {
    // Treat 403 like 401 per the dashboard contract.
    return this.status === 401 || this.status === 403;
  }

  /**************************************************************/
  /**
   * Indicates that the AE dashboard feature flag is disabled.
   *
   * @returns {boolean} True when the status is 503.
   */
  get isFeatureDisabled() {
    // A 503 from this API surface means the dashboard is configured off.
    return this.status === 503;
  }
}

/**************************************************************/
/**
 * Reads a server response body into the best available display message.
 *
 * @param {Response} response - Fetch response to inspect.
 * @returns {Promise<{ message: string, details: unknown }>} Parsed message and details.
 */
export async function readErrorPayload(response) {
  // The details value stays null unless JSON or text parsing succeeds.
  let details = null;

  // The fallback message gives every status a stable user-facing string.
  let message = `Request failed with status ${response.status}.`;

  try {
    // The content type decides whether we should parse JSON or plain text.
    const contentType = response.headers.get('content-type') ?? '';

    // JSON API errors can carry strings, ProblemDetails objects, or arrays.
    if (contentType.includes('application/json')) {
      details = await response.json();

      // ProblemDetails uses title/detail; string bodies are already readable.
      if (typeof details === 'string') {
        message = details;
      } else if (details?.detail) {
        message = details.detail;
      } else if (details?.title) {
        message = details.title;
      }
    } else {
      // Plain-text controller errors are common in the existing API.
      const text = await response.text();

      // Only replace the fallback when the server supplied non-empty text.
      if (text.trim().length > 0) {
        details = text;
        message = text;
      }
    }
  } catch {
    // Parsing failures leave the fallback status-based message intact.
  }

  return { message, details };
}
