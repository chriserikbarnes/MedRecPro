using MedRecPro.Models;

namespace MedRecPro.Service;

/**************************************************************/
/// <summary>
/// Resolves endpoint monitoring policy for a request path.
/// </summary>
/// <remarks>
/// The resolver centralizes endpoint include, exclusion, default, and legacy
/// fallback behavior so the middleware uses the same policy decision before
/// and after controller execution.
/// </remarks>
/// <seealso cref="TarpitSettings"/>
/// <seealso cref="EndpointMonitoringSettings"/>
/// <seealso cref="TarpitEndpointPolicy"/>
public static class TarpitEndpointPolicyResolver
{
    /**************************************************************/
    /// <summary>
    /// Resolves the endpoint monitoring policy for the supplied request path.
    /// </summary>
    /// <param name="requestPath">The reconstructed request path to evaluate.</param>
    /// <param name="settings">The current tarpit settings snapshot.</param>
    /// <returns>
    /// A resolved endpoint policy when the path is monitored; otherwise <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Excluded prefixes are checked before include rules. If no new endpoint
    /// rules are configured, legacy <see cref="TarpitSettings.MonitoredEndpoints"/>
    /// prefixes are translated into policies for backward compatibility.
    /// </remarks>
    /// <seealso cref="TarpitEndpointPolicy"/>
    /// <seealso cref="TarpitEndpointRule"/>
    public static TarpitEndpointPolicy? Resolve(string requestPath, TarpitSettings settings)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(requestPath))
            return null;

        var endpointSettings = settings.EndpointMonitoring;
        if (endpointSettings == null || !endpointSettings.Enabled)
            return null;

        if (isExcluded(requestPath, endpointSettings.ExcludedPathPrefixes))
            return null;

        var enabledRules = endpointSettings.Rules
            .Where(rule => rule.Enabled)
            .ToList();

        if (enabledRules.Count > 0)
        {
            foreach (var rule in enabledRules)
            {
                if (string.IsNullOrWhiteSpace(rule.PathPrefix))
                    continue;

                if (requestPath.StartsWith(rule.PathPrefix, StringComparison.OrdinalIgnoreCase))
                    return createPolicy(rule, endpointSettings);
            }

            return null;
        }

        return resolveLegacyPolicy(requestPath, settings);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether a request path matches an excluded prefix.
    /// </summary>
    /// <param name="requestPath">The reconstructed request path.</param>
    /// <param name="excludedPathPrefixes">The configured endpoint exclusions.</param>
    /// <returns><c>true</c> when an exclusion prefix matches; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Exclusion matching is case-insensitive and runs before new or legacy
    /// endpoint include rules.
    /// </remarks>
    /// <seealso cref="EndpointMonitoringSettings.ExcludedPathPrefixes"/>
    private static bool isExcluded(string requestPath, List<string> excludedPathPrefixes)
    {
        #region implementation

        foreach (var prefix in excludedPathPrefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                continue;

            if (requestPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates an effective endpoint policy from a configured endpoint rule.
    /// </summary>
    /// <param name="rule">The matched endpoint rule.</param>
    /// <param name="settings">The endpoint monitoring defaults.</param>
    /// <returns>The resolved endpoint policy.</returns>
    /// <remarks>
    /// Rule-level threshold, window, and delay values override defaults only
    /// when supplied.
    /// </remarks>
    /// <seealso cref="TarpitEndpointRule"/>
    /// <seealso cref="EndpointMonitoringSettings"/>
    private static TarpitEndpointPolicy createPolicy(
        TarpitEndpointRule rule,
        EndpointMonitoringSettings settings)
    {
        #region implementation

        return new TarpitEndpointPolicy
        {
            Name = normalizePolicyName(rule.Name),
            PathPrefix = rule.PathPrefix,
            RateThreshold = Math.Max(1, rule.RateThreshold ?? settings.DefaultRateThreshold),
            WindowSeconds = Math.Max(1, rule.WindowSeconds ?? settings.DefaultWindowSeconds),
            MaxDelayMs = Math.Max(0, rule.MaxDelayMs ?? settings.DefaultMaxDelayMs)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves a legacy endpoint policy when no new endpoint rules are configured.
    /// </summary>
    /// <param name="requestPath">The reconstructed request path.</param>
    /// <param name="settings">The current tarpit settings snapshot.</param>
    /// <returns>
    /// A legacy endpoint policy when a configured legacy prefix matches;
    /// otherwise <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Legacy policies use the normalized path prefix as the stable key to
    /// preserve existing tracker behavior and tests.
    /// </remarks>
    /// <seealso cref="TarpitSettings.MonitoredEndpoints"/>
    /// <seealso cref="TarpitSettings.EndpointRateThreshold"/>
    private static TarpitEndpointPolicy? resolveLegacyPolicy(string requestPath, TarpitSettings settings)
    {
        #region implementation

        foreach (var endpoint in settings.MonitoredEndpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                continue;

            if (!requestPath.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase))
                continue;

            var normalizedEndpoint = normalizePolicyName(endpoint);
            return new TarpitEndpointPolicy
            {
                Name = normalizedEndpoint,
                PathPrefix = endpoint,
                RateThreshold = Math.Max(1, settings.EndpointRateThreshold),
                WindowSeconds = Math.Max(1, settings.EndpointWindowSeconds),
                MaxDelayMs = Math.Max(0, settings.MaxDelayMs)
            };
        }

        return null;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Normalizes endpoint policy names used as tracker keys.
    /// </summary>
    /// <param name="name">The configured policy name or legacy path prefix.</param>
    /// <returns>A trimmed, lower-case policy key.</returns>
    /// <remarks>
    /// Normalization prevents case-only configuration edits from creating new
    /// endpoint tracker buckets.
    /// </remarks>
    /// <seealso cref="TarpitEndpointPolicy.Name"/>
    private static string normalizePolicyName(string name)
    {
        #region implementation

        return name.Trim().ToLowerInvariant();

        #endregion
    }
}
