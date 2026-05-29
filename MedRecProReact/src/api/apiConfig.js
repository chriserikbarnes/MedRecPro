// The public API path is stable in production because the API runs as /api.
const productionApiBasePath = '/api/AdverseEvent';

// The Vite dev server talks to the local MedRecPro API HTTP profile directly.
const localDevelopmentApiBaseUrl = 'http://localhost:5093/api/AdverseEvent';

// The static MVC site's HTTPS profile needs the API's HTTPS profile to avoid mixed content.
const localDevelopmentSecureApiBaseUrl = 'https://localhost:7201/api/AdverseEvent';

// Local MedRecProStatic launch ports do not host /api themselves.
const localStaticHostPorts = new Set(['5001', '7199', '30582', '44318']);

/**************************************************************/
/**
 * Resolves the adverse-event API base URL for standalone Vite or MVC-hosted use.
 *
 * @returns {string} Absolute dev URL or same-origin production path.
 */
export function getAdverseEventApiBase() {
  // The location object is guarded so unit tests and non-browser tools can import this module.
  const browserLocation = globalThis.window?.location;

  // Non-browser execution should use the production path because it is side-effect free.
  if (!browserLocation) {
    return productionApiBasePath;
  }

  // Vite serves this app on the fixed port specified in vite.config.js.
  const isViteDevelopmentPort = browserLocation.port === '50346';

  // MedRecProStatic serves the integrated dashboard page on its own local ports.
  const isLocalStaticHostPort = localStaticHostPorts.has(browserLocation.port);

  // Localhost and 127.0.0.1 are the only standalone dev hosts expected here.
  const isLocalDevelopmentHost =
    browserLocation.hostname === 'localhost' || browserLocation.hostname === '127.0.0.1';

  // Standalone Vite and HTTP static-host development cross origins to the API on port 5093.
  if (isLocalDevelopmentHost && (isViteDevelopmentPort || isLocalStaticHostPort) && browserLocation.protocol === 'http:') {
    return localDevelopmentApiBaseUrl;
  }

  // HTTPS static-host development must call the HTTPS API profile to avoid mixed-content blocking.
  if (isLocalDevelopmentHost && isLocalStaticHostPort && browserLocation.protocol === 'https:') {
    return localDevelopmentSecureApiBaseUrl;
  }

  return productionApiBasePath;
}

/**************************************************************/
/**
 * Builds fetch options shared by every AE dashboard request.
 *
 * @param {AbortSignal | null} signal - Optional cancellation signal.
 * @param {RequestInit} overrides - Optional fetch overrides.
 * @returns {RequestInit} Fetch options with credentials enabled.
 */
export function getFetchOptions(signal = null, overrides = {}) {
  // Start with the API contract defaults.
  const options = {
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(overrides.headers ?? {}),
    },
    ...overrides,
  };

  // Only attach a signal when a caller supplied one.
  if (signal) {
    options.signal = signal;
  }

  return options;
}
