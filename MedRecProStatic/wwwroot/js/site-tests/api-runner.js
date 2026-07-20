/**************************************************************/
/**
 * MedRecPro API Test Runtime Module
 *
 * @fileoverview Owns safe browser-origin transport, run state, reporting, cleanup, profile filtering, and Phase 0–4 orchestration for the endpoint diagnostic.
 *
 * @description Consumes the audited manifest and panel factory, while allowing phase scripts to register definitions through a narrow runtime surface.
 *
 * @module site-tests/api-runner
 * @see MedRecProApiTestManifest
 * @see MedRecProApiTestPanel
 */
/**************************************************************/
window.MedRecProApiTestRuntime = (function (manifest, panelModule) {
    'use strict';
    if (!manifest || !panelModule) { throw new Error('MedRecPro API test manifest and panel modules must load before the runtime.'); }
    var AUDIT_METADATA = manifest.auditMetadata;
    var AUDITED_OPERATION_MANIFEST = manifest.operations;
    var panel = null;
    /**************************************************************/
    /**
     * Lazily connects the presentation module to runtime callbacks.
     *
     * @returns {Object} API diagnostic panel controller.
     */
    /**************************************************************/
    function getPanel() { if (!panel) { panel = panelModule.create({ run: runApiTests, cancel: cancelActiveRun, getLastReport: function () { return lastReport; }, getRedactedReport: function () { return lastReport ? redactReport(lastReport) : null; } }); } return panel; }
    /**************************************************************/
    /**
     * Removes response bodies and identity-like values before a report is copied from the panel.
     *
     * @param {Object} report Completed API diagnostic report.
     * @returns {Object} Safe-to-copy report clone.
     */
    /**************************************************************/
    function redactReport(report) { var copy = JSON.parse(JSON.stringify(report)); copy.tests.forEach(function (test) { delete test.bodyExcerpt; if (test.url) test.url = test.path || '[redacted-url]'; }); return copy; }
    var TEST_REGISTRY = [];
    var activeRun = null;
    var lastReport = null;
    var queryTriggerInitialized = false;
    var COST_OR_MUTATION_CONFIRMATION = 'RUN CONFIRMED COST OR MUTATION';
    var DISPOSABLE_DATA_CONFIRMATION = 'DISPOSABLE LOCAL DATABASE CONFIRMED';
    var LOCALLY_EXPOSED_RESPONSE_HEADERS = ['x-page-number', 'x-page-size', 'x-total-count', 'x-chartable-count'];
    var LOCAL_DEBUG_INTER_REQUEST_DELAY_MS = 0;
    var ONLINE_INTER_REQUEST_DELAY_MS = 5000;

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function isLoopbackHost() {
        var hostname = window.location.hostname;
        return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1';
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function isLocalDevelopment() {
        var hostname = window.location.hostname;
        return isLoopbackHost() || hostname.indexOf('192.168.') === 0 || hostname.indexOf('10.') === 0;
    }

    /**************************************************************/
    /**
     * Determines whether the page and selected API target represent a local Debug run.
     * @param {string} apiBase API base URL selected for the diagnostic.
     * @returns {boolean} True when no online pacing should be added.
     */
    /**************************************************************/
    function isLocalDebugTarget(apiBase) {
        if (!isLocalDevelopment()) return false;
        try {
            var targetHostname = new URL(apiBase || window.location.origin).hostname;
            return targetHostname === window.location.hostname || targetHostname === 'localhost' || targetHostname === '127.0.0.1' || targetHostname === '::1';
        } catch (error) {
            return false;
        }
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function getApiBase(option) {
        if (typeof option === 'string' && option.trim()) return option.trim().replace(/\/$/, '');
        return isLoopbackHost() ? 'http://localhost:5093' : '';
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport. 
     * @private
     * 
     */

    /**************************************************************/

    function getDefaultOptions(options) {
        var defaults = {
            apiBase: null,
            tarpitMode: 'unknown',
            includeAi: false,
            includeMutating: false,
            includeCacheClear: false,
            includeAdminWrites: false,
            includeImport: false,
            includeLogout: false,
            includeSlow: false,
            conversationLifecycle: isLoopbackHost(),
            confirmations: { costOrMutation: null, disposableData: null },
            importFile: null,
            sectionFixture: null,
            interRequestDelayMs: null,
            requestTimeoutMs: 30000,
            stopOnFirstFail: false,
            groups: null,
            categories: null,
            families: null,
            testIds: null,
            seedTestIds: null,
            runFullPreflight: null,
            requireAnonymous: false,
            profile: 'anonymous',
            selection: { source: 'console' }
        };
        var merged = Object.assign({}, defaults, options || {});
        merged.confirmations = Object.assign({}, defaults.confirmations, (options || {}).confirmations || {});
        merged.tarpitMode = ['unknown', 'active', 'disabled'].indexOf(merged.tarpitMode) >= 0
            ? merged.tarpitMode
            : 'unknown';
        merged.apiBase = getApiBase(merged.apiBase);
        merged.localDebug = isLocalDebugTarget(merged.apiBase);
        merged.interRequestDelayMs = merged.localDebug ? LOCAL_DEBUG_INTER_REQUEST_DELAY_MS : ONLINE_INTER_REQUEST_DELAY_MS;
        merged.pacingLabel = merged.localDebug
            ? 'Local Debug: no artificial delay between calls.'
            : 'Online: 5 seconds between calls.';
        if (typeof (options || {}).runFullPreflight !== 'boolean') {
            var selectionSource = (merged.selection && merged.selection.source) || defaults.selection.source;
            merged.runFullPreflight = selectionSource === 'console' || selectionSource === 'query';
        }
        return merged;
    }

    /**************************************************************/
    /**
     * Checks the exact operator confirmation phrases required before paid or durable probes run.
     *
     * @param {Object} run Active diagnostic state.
     * @param {boolean} requiresDisposableData Whether the durable-data attestation is additionally required.
     * @returns {boolean} True only when the relevant operator confirmations are exact.
     */
    /**************************************************************/
    function hasRequiredConfirmation(run, requiresDisposableData) {
        var confirmations = run.options.confirmations || {};
        var costOrMutation = String(confirmations.costOrMutation || '').trim() === COST_OR_MUTATION_CONFIRMATION;
        var disposable = String(confirmations.disposableData || '').trim() === DISPOSABLE_DATA_CONFIRMATION;
        return costOrMutation && (!requiresDisposableData || disposable);
    }
    /**************************************************************/
    /**
     * Computes the SHA-256 identity of an operator-selected import fixture without uploading it.
     *
     * @param {File} importFile Selected disposable SPL ZIP fixture.
     * @returns {Promise<string|null>} Lowercase SHA-256 hex digest, or null when Web Crypto is unavailable.
     */
    /**************************************************************/
    async function getImportFixtureSha256(importFile) {
        if (!importFile || !window.crypto || !window.crypto.subtle || typeof importFile.arrayBuffer !== 'function') return null;
        var digest = await window.crypto.subtle.digest('SHA-256', await importFile.arrayBuffer());
        return Array.prototype.map.call(new Uint8Array(digest), function (value) { return value.toString(16).padStart(2, '0'); }).join('');
    }

    function isPhaseFourCategorySelected(run, definition) {
        var category = definition.category || '';
        if (Array.isArray(run.options.categories) && run.options.categories.length) return run.options.categories.indexOf(category) >= 0;
        return (category === 'ai' && run.options.includeAi) ||
            (category === 'mutating' && run.options.includeMutating) ||
            (category === 'adminWrite' && run.options.includeAdminWrites) ||
            (category === 'upload' && run.options.includeImport) ||
            (category === 'slow' && run.options.includeSlow) ||
            (category === 'logout' && run.options.includeLogout);
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function toUrl(path, apiBase, query) {
        var url = new URL(path, apiBase || window.location.origin);
        Object.keys(query || {}).forEach(function (key) {
            var value = query[key];
            if (value === null || typeof value === 'undefined') return;
            if (Array.isArray(value)) {
                value.forEach(function (item) {
                    if (item !== null && typeof item !== 'undefined') url.searchParams.append(key, item);
                });
            } else {
                url.searchParams.set(key, value);
            }
        });
        return url;
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function normalizeOperationPath(path) {
        var normalized = '/' + String(path || '').replace(/^\/+/, '').replace(/\/{2,}/g, '/');
        normalized = normalized.replace(/:[^}/]+(?=})/g, '');
        return normalized.toLowerCase().indexOf('/api/') === 0 || normalized.toLowerCase() === '/api'
            ? normalized
            : '/api' + normalized;
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function normalizeOperationKey(method, path) {
        return String(method || '').toUpperCase() + ' ' + normalizeOperationPath(path).toLowerCase();
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport. 
     * @private
     * 
     */

    /**************************************************************/

    function hasValue(value) {
        return value !== null && typeof value !== 'undefined' && String(value).trim() !== '';
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function getProperty(value, names) {
        if (!value || typeof value !== 'object') return null;
        var expected = names.map(function (name) { return String(name).toLowerCase(); });
        var key = Object.keys(value).find(function (candidate) {
            return expected.indexOf(candidate.toLowerCase()) >= 0;
        });
        return typeof key === 'undefined' ? null : value[key];
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function findValue(value, names, depth) {
        if (depth < 0 || value === null || typeof value === 'undefined') return null;
        if (Array.isArray(value)) {
            for (var arrayIndex = 0; arrayIndex < value.length; arrayIndex++) {
                var arrayValue = findValue(value[arrayIndex], names, depth - 1);
                if (arrayValue !== null && typeof arrayValue !== 'undefined') return arrayValue;
            }
            return null;
        }
        if (typeof value !== 'object') return null;
        var direct = getProperty(value, names);
        if (direct !== null && typeof direct !== 'undefined') return direct;
        var keys = Object.keys(value);
        for (var keyIndex = 0; keyIndex < keys.length; keyIndex++) {
            var nested = findValue(value[keys[keyIndex]], names, depth - 1);
            if (nested !== null && typeof nested !== 'undefined') return nested;
        }
        return null;
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport
     * @private
     * 
     */

    /**************************************************************/

    function firstItem(body) {
        if (Array.isArray(body)) return body.length ? body[0] : null;
        return findValue(body, ['items', 'results', 'data', 'value'], 1) || null;
    }

    /**************************************************************/

    /**
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport
     * @private
     * 
     */

    /**************************************************************/

    function redactText(value) {
        return String(value || '')
            .replace(/[A-Za-z0-9_-]{24,}/g, '[redacted]')
            .replace(/[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}/g, '[redacted-email]')
            .replace(/("(?:encrypted\w*id|email|name|token|message|content)"\s*:\s*)"[^"]*"/gi, '$1"[redacted]"');
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function excerpt(value) {
        return redactText(value).slice(0, 600);
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     */

    /**************************************************************/

    function defineTest(definition) {
        TEST_REGISTRY.push(definition);
        return definition;
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function createAssertion(name, outcome, detail) {
        return { name: name, outcome: outcome, detail: detail || '' };
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function toHeaderMap(headers) {
        var values = {};
        headers.forEach(function (value, key) { values[key.toLowerCase()] = value; });
        return values;
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function expectStatus(response, statuses) {
        var expected = Array.isArray(statuses) ? statuses : [statuses];
        return createAssertion(
            'HTTP status',
            expected.indexOf(response.status) >= 0 ? 'pass' : 'fail',
            'Expected ' + expected.join(' or ') + '; received ' + (response.transportError || response.status)
        );
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function expectJsonArray(response) {
        return createAssertion(
            'JSON array response',
            Array.isArray(response.body) ? 'pass' : 'fail',
            Array.isArray(response.body) ? 'Received array.' : 'Expected a JSON array.'
        );
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function expectJsonObject(response) {
        var passed = !!response.body && !Array.isArray(response.body) && typeof response.body === 'object';
        return createAssertion('JSON object response', passed ? 'pass' : 'fail', passed ? 'Received object.' : 'Expected a JSON object.');
    }
    /**************************************************************/
    /**
     * Verifies a numeric JSON response without treating zero as absent.
     *
     * @param {Object} response Browser transport result.
     * @returns {Object} Named assertion result.
     */
    /**************************************************************/
    function expectJsonNumber(response) {
        var passed = typeof response.body === 'number' && Number.isFinite(response.body);
        return createAssertion('JSON numeric response', passed ? 'pass' : 'fail', passed ? 'Received a finite number.' : 'Expected a finite JSON number.');
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function expectFields(value, fields) {
        var missing = fields.filter(function (field) { return !hasValue(findValue(value, [field], 3)); });
        return createAssertion(
            'Expected fields',
            missing.length ? 'fail' : 'pass',
            missing.length ? 'Missing: ' + missing.join(', ') : 'All expected fields present.'
        );
    }
    /**************************************************************/
    /**
     * Determines whether the selected API base is a loopback host.
     * @param {string} apiBase API base URL to inspect.
     * @returns {boolean} True for localhost loopback targets.
     */
    /**************************************************************/
    function isLoopbackApiTarget(apiBase) {
        try {
            var hostname = new URL(apiBase || window.location.origin).hostname;
            return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1';
        } catch (error) {
            return false;
        }
    }

    /**
     * Determines whether a custom response header is observable in the current browser context.
     * @param {Object} run Active test run.
     * @param {string} name Header name.
     * @returns {boolean} True when same-origin or a documented local CORS header is exposed.
     */
    function canObserveResponseHeader(run, name) {
        return run.report.environment.sameOrigin || (
            isLocalDevelopment() &&
            isLoopbackApiTarget(run.options.apiBase) &&
            LOCALLY_EXPOSED_RESPONSE_HEADERS.indexOf(String(name).toLowerCase()) >= 0
        );
    }

    /**
     * Verifies a custom response header only when the browser can observe it.
     * @param {Object} run Active test run.
     * @param {Object} response Browser transport result.
     * @param {string} name Header name.
     * @returns {Object} Named assertion result.
     */
    function expectObservableHeader(run, response, name) {
        if (!canObserveResponseHeader(run, name)) {
            return createAssertion(name + ' header', 'notObservable', 'Custom response headers are not asserted across origins.');
        }
        var value = response.headers && response.headers[String(name).toLowerCase()];
        return createAssertion(name + ' header', hasValue(value) ? 'pass' : 'fail', hasValue(value) ? 'Header is present.' : 'Expected header was missing.');
    }

    /**************************************************************/
    /**
     * Verifies the paging and chartable headers exposed by same-origin APIs.
     *
     * @param {Object} run Active test run.
     * @param {Object} response Browser transport result.
     * @param {boolean} [includeChartableCount] Whether X-Chartable-Count is required.
     * @returns {Object[]} Named assertion results.
     */
    /**************************************************************/
    function expectPagedHeaders(run, response, includeChartableCount) {
        var names = ['X-Page-Number', 'X-Page-Size', 'X-Total-Count'];
        if (includeChartableCount) names.push('X-Chartable-Count');
        return names.map(function (name) { return expectObservableHeader(run, response, name); });
    }

    /**************************************************************/
    /**
     * Verifies a response content type using the CORS-safelisted header.
     *
     * @param {Object} response Browser transport result.
     * @param {string} prefix Expected media type prefix.
     * @returns {Object} Named assertion result.
     */
    /**************************************************************/
    function expectContentType(response, prefix) {
        var passed = String(response.contentType || '').toLowerCase().indexOf(String(prefix).toLowerCase()) === 0;
        return createAssertion('Content-Type', passed ? 'pass' : 'fail', passed ? 'Received ' + response.contentType + '.' : 'Expected ' + prefix + '; received ' + (response.contentType || '(none)') + '.');
    }

    /**************************************************************/
    /**
     * Checks that a nonempty response body can be parsed as XML.
     *
     * @param {Object} response Browser transport result.
     * @returns {Object} Named assertion result.
     */
    /**************************************************************/
    function expectXmlDocument(response) {
        var documentNode = new DOMParser().parseFromString(response.bodyText || '', 'application/xml');
        var hasParserError = documentNode.getElementsByTagName('parsererror').length > 0;
        var passed = !hasParserError && !!documentNode.documentElement && documentNode.documentElement.nodeName !== 'parsererror';
        return createAssertion('XML document', passed ? 'pass' : 'fail', passed ? 'XML document parsed successfully.' : 'Response was not valid XML.');
    }

    /**************************************************************/
    /**
     * Verifies attachment semantics while retaining cross-origin observability limits.
     *
     * @param {Object} run Active test run.
     * @param {Object} response Browser transport result.
     * @returns {Object} Named assertion result.
     */
    /**************************************************************/
    function expectAttachment(run, response) {
        if (!run.report.environment.sameOrigin) {
            return createAssertion('Content-Disposition attachment', 'notObservable', 'Content-Disposition is not asserted across origins.');
        }
        var disposition = response.headers && response.headers['content-disposition'];
        var passed = /attachment/i.test(disposition || '');
        return createAssertion('Content-Disposition attachment', passed ? 'pass' : 'fail', passed ? 'Attachment disposition is present.' : 'Expected attachment disposition.');
    }

    /**************************************************************/
    /**
     * Verifies the manual OAuth redirect response without following it.
     *
     * @param {Object} response Browser transport result.
     * @returns {Object} Named assertion result.
     */
    /**************************************************************/
    function expectRedirectProbe(response) {
        var opaqueRedirect = response.type === 'opaqueredirect' && response.status === 0;
        var serviceDisabled = response.status === 503;
        return createAssertion('Manual redirect probe', opaqueRedirect || serviceDisabled ? 'pass' : 'fail', opaqueRedirect ? 'Received opaque manual redirect.' : serviceDisabled ? 'External authentication is disabled (503).' : 'Expected opaque manual redirect or disabled-service 503.');
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    function createRun(options) {
        var apiOrigin = new URL(options.apiBase || window.location.origin).origin;
        return {
            options: options,
            controller: new AbortController(),
            context: {},
            cleanupStack: [],
            report: {
                startedAt: new Date().toISOString(),
                completedAt: null,
                durationMs: 0,
                cancelled: false,
                blocked: null,
                options: Object.assign({}, options, {
                    confirmations: undefined,
                    importFile: options.importFile ? { name: options.importFile.name, size: options.importFile.size, type: options.importFile.type } : null
                }),
                environment: {
                    pageOrigin: window.location.origin,
                    apiBase: options.apiBase || '(same-origin)',
                    sameOrigin: apiOrigin === window.location.origin,
                    authenticated: null,
                    isAdmin: false,
                    featureFlags: null,
                    tarpitMode: options.tarpitMode,
                    tarpitModeAttestation: options.tarpitMode === 'disabled' ? 'operator-confirmed' : 'not-attested'
                },
                manifest: {
                    auditedAt: AUDIT_METADATA.auditedAt,
                    sourceRevision: AUDIT_METADATA.sourceRevision,
                    operations: AUDITED_OPERATION_MANIFEST.slice()
                },
                openApi: {
                    url: null,
                    outcome: 'pending',
                    discovered: [],
                    registered: AUDITED_OPERATION_MANIFEST.slice(),
                    missingFromRegistry: [],
                    staleRegistry: []
                },
                endpointCoverage: {
                    accounted: AUDITED_OPERATION_MANIFEST.length,
                    invoked: 0,
                    positive: 0,
                    negativeOnly: 0,
                    authGateOnly: 0,
                    featureGateOnly: 0,
                    skippedOrInconclusive: 0,
                    notObservable: 0,
                    safetyExcluded: 0
                },
                cleanup: { attempted: 0, succeeded: 0, failed: 0, details: [] },
                cost: { declaredMaximumAiCalls: 8, positiveAiCalls: 0, warning: 'Paid AI calls remain disabled until the exact confirmation phrase is supplied.' },
                fixtures: { import: options.importFile ? { name: options.importFile.name, size: options.importFile.size, type: options.importFile.type, sha256: null } : null, section: options.sectionFixture || null },
                findings: [],
                summary: { total: 0, passed: 0, failed: 0, skipped: 0 },
                groups: [],
                tests: [],
                selection: options.selection
            }
        };
    }

    /**************************************************************/

    /**
     * 
     * Supports safe browser-origin API diagnostic configuration, normalization, and transport.
     * @private
     * 
     */

    /**************************************************************/

    async function apiFetch(run, spec) {
        var url = toUrl(spec.path, run.options.apiBase, spec.query);
        var started = performance.now();
        await waitForInterRequestDelay(run);
        if (run.controller.signal.aborted) {
            return {
                url: url.toString(), status: 0, ok: false, headers: null, contentType: '', body: null, bodyText: '',
                redirected: false, type: null, durationMs: Math.round(performance.now() - started), transportError: 'aborted',
                error: 'Run cancelled before request pacing completed.'
            };
        }
        var requestController = new AbortController();
        var requestTimeoutMs = spec.requestTimeoutMs || run.options.requestTimeoutMs;
        var timeoutId = window.setTimeout(function () { requestController.abort('timeout'); }, requestTimeoutMs);
        var abortFromRun = function () { requestController.abort('aborted'); };
        run.controller.signal.addEventListener('abort', abortFromRun, { once: true });
        var requestOptions = {
            method: spec.method || 'GET',
            credentials: spec.credentials || 'include',
            redirect: spec.redirect || 'follow',
            signal: requestController.signal,
            headers: Object.assign({ Accept: 'application/json' }, spec.headers || {})
        };
        if (spec.formData) {
            requestOptions.body = spec.formData;
        } else if (spec.body !== null && typeof spec.body !== 'undefined') {
            requestOptions.body = typeof spec.body === 'string' ? spec.body : JSON.stringify(spec.body);
            if (!requestOptions.headers['Content-Type']) requestOptions.headers['Content-Type'] = 'application/json';
        }

        return fetch(url.toString(), requestOptions)
            .then(function (response) {
                var contentType = response.headers.get('content-type') || '';
                return response.text().then(function (bodyText) {
                    var body = bodyText;
                    if (contentType.toLowerCase().indexOf('json') >= 0 && bodyText) {
                        try { body = JSON.parse(bodyText); } catch (error) { body = bodyText; }
                    }
                    return {
                        url: url.toString(),
                        status: response.status,
                        ok: response.ok,
                        headers: toHeaderMap(response.headers),
                        contentType: contentType,
                        body: body,
                        bodyText: bodyText,
                        redirected: response.redirected,
                        type: response.type,
                        durationMs: Math.round(performance.now() - started),
                        transportError: null
                    };
                });
            })
            .catch(function (error) {
                var reason = requestController.signal.reason;
                return {
                    url: url.toString(), status: 0, ok: false, headers: null, contentType: '', body: null, bodyText: '',
                    redirected: false, type: null, durationMs: Math.round(performance.now() - started),
                    transportError: reason === 'timeout' ? 'timeout' : run.controller.signal.aborted ? 'aborted' : 'network-or-cors',
                    error: error && error.message ? error.message : String(error)
                };
            })
            .finally(function () {
                window.clearTimeout(timeoutId);
                run.controller.signal.removeEventListener('abort', abortFromRun);
            });
    }

    /**************************************************************/
    /**
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     *
     * @private
     */
    /**************************************************************/
    function refreshReport(run) {
        var tests = run.report.tests;
        var summary = { total: tests.length, passed: 0, failed: 0, skipped: 0 };
        var coverage = {
            accounted: AUDITED_OPERATION_MANIFEST.length,
            invoked: 0, positive: 0, negativeOnly: 0, authGateOnly: 0, featureGateOnly: 0,
            skippedOrInconclusive: 0, notObservable: 0, safetyExcluded: 0
        };
        var groups = {};
        tests.forEach(function (test) {
            if (test.outcome === 'pass') summary.passed++;
            if (test.outcome === 'fail') summary.failed++;
            if (test.outcome === 'skip') summary.skipped++;
            if (test.invoked) coverage.invoked++;
            if (test.outcome === 'skip') coverage.skippedOrInconclusive++;
            if (test.evidenceKind === 'safetyExcluded') coverage.safetyExcluded++;
            if (test.evidenceKind === 'positive' && test.positiveContractVerified) coverage.positive++;
            if (test.evidenceKind === 'negative') coverage.negativeOnly++;
            if (test.evidenceKind === 'authGate') coverage.authGateOnly++;
            if (test.evidenceKind === 'featureGate') coverage.featureGateOnly++;
            if (test.assertions.some(function (assertion) { return assertion.outcome === 'notObservable'; })) coverage.notObservable++;
            if (!groups[test.group]) groups[test.group] = { name: test.group, passed: 0, failed: 0, skipped: 0 };
            if (test.outcome === 'pass') groups[test.group].passed++;
            if (test.outcome === 'fail') groups[test.group].failed++;
            if (test.outcome === 'skip') groups[test.group].skipped++;
        });
        run.report.summary = summary;
        run.report.endpointCoverage = coverage;
        run.report.groups = Object.keys(groups).map(function (key) { return groups[key]; });
        run.report.durationMs = Math.round(Date.now() - new Date(run.report.startedAt).getTime());
    }

    /**************************************************************/

    /**
     * 
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     * @private
     * 
     */

    /**************************************************************/

    function createRecord(definition) {
        return {
            id: definition.id,
            operationKey: definition.operationKey || null,
            evidenceKind: definition.evidenceKind || 'positive',
            group: definition.group || 'Infrastructure',
            name: definition.name,
            method: definition.method || null,
            path: definition.path || null,
            url: null,
            outcome: 'running',
            positiveContractVerified: false,
            httpStatus: null,
            expectedStatus: definition.expectedStatus || null,
            durationMs: 0,
            transportError: null,
            assertions: [],
            skipReason: null,
            bodyExcerpt: null,
            invoked: false,
            note: definition.note || null
        };
    }

    /**************************************************************/

    /**
     * 
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     * @private
     * 
     */

    /**************************************************************/

    function settleRecord(run, record, outcome, result) {
        record.outcome = outcome;
        record.assertions = (result && result.assertions) || record.assertions;
        record.skipReason = (result && result.skipReason) || null;
        record.positiveContractVerified = !!(result && result.positiveContractVerified);
        if (result && result.evidenceKind) record.evidenceKind = result.evidenceKind;
        if (result && result.note) record.note = record.note ? record.note + ' ' + result.note : result.note;
        if (result && result.response) {
            record.invoked = true;
            record.url = result.response.url;
            record.httpStatus = result.response.status || null;
            record.durationMs = result.response.durationMs;
            record.transportError = result.response.transportError;
            record.bodyExcerpt = excerpt(result.response.bodyText);
        }
        refreshReport(run);
        getPanel().renderRecord(record);
        getPanel().renderRun(run);
        console.log('%c  ' + outcome.toUpperCase() + '  %s', outcome === 'pass' ? 'color: green' : outcome === 'fail' ? 'color: red; font-weight: bold' : 'color: #a78327', record.name);
    }

    /**************************************************************/

    /**
     * 
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     * @private
     * 
     */

    /**************************************************************/

    function responseResult(response, options) {
        var assertions = [];
        if (response.transportError) {
            assertions.push(createAssertion('Transport', 'fail', response.transportError));
            return { outcome: response.transportError === 'aborted' ? 'skip' : 'fail', assertions: assertions, skipReason: response.transportError === 'aborted' ? 'Run cancelled.' : null, response: response };
        }
        assertions.push(expectStatus(response, options.status));
        if (options.jsonArray) assertions.push(expectJsonArray(response));
        if (options.jsonObject) assertions.push(expectJsonObject(response));
        if (options.fields) assertions.push(expectFields(response.body, options.fields));
        return {
            outcome: assertions.some(function (assertion) { return assertion.outcome === 'fail'; }) ? 'fail' : 'pass',
            assertions: assertions,
            positiveContractVerified: assertions.every(function (assertion) { return assertion.outcome === 'pass'; }),
            response: response
        };
    }

    /**************************************************************/

    /**
     * 
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     * @private
     * 
     */

    /**************************************************************/

    function getDefinitionCategory(definition) {
        if (definition.category) return definition.category;
        if (definition.phase === 2) return 'read';
        if (definition.phase === 3) return definition.evidenceKind === 'authGate' ? 'authGate' : 'contract';
        if (definition.id && definition.id.indexOf('seed.ai.conversation') === 0) return 'safeWrite';
        return definition.phase === 0 ? 'preflight' : 'seed';
    }

    function getDefinitionFamilies(definition) {
        var value = String(definition.operationKey || definition.path || definition.id || '').toLowerCase();
        if (value.indexOf('/adverseevent') >= 0 || value.indexOf('.ae.') >= 0) return ['ae'];
        if (value.indexOf('/orangebook') >= 0 || value.indexOf('.orangebook.') >= 0) return ['orangebook'];
        if (value.indexOf('/label') >= 0 || value.indexOf('.label.') >= 0) return ['label'];
        if (value.indexOf('/settings') >= 0 || value.indexOf('.settings.') >= 0) return ['settings'];
        if (value.indexOf('/ai/conversations') >= 0 || value.indexOf('.ai.conversation') >= 0) return ['ai', 'conversations'];
        if (value.indexOf('/ai') >= 0 || value.indexOf('.ai.') >= 0) return ['ai'];
        if (value.indexOf('/users') >= 0 || value.indexOf('.users.') >= 0) return ['users'];
        if (value.indexOf('/auth') >= 0 || value.indexOf('.auth.') >= 0) return ['auth'];
        return ['preflight'];
    }

    function intersects(left, right) {
        return left.some(function (value) { return right.indexOf(value) >= 0; });
    }

    function shouldRunTest(run, definition) {
        if (definition.phase === 4) return isPhaseFourCategorySelected(run, definition);
        if (definition.phase === 0) return false;
        if (definition.phase === 1) {
            if (Array.isArray(run.options.seedTestIds) && run.options.seedTestIds.length) return run.options.seedTestIds.indexOf(definition.id) >= 0;
            if (Array.isArray(run.options.families) && run.options.families.length) return intersects(getDefinitionFamilies(definition), run.options.families);
            return !(Array.isArray(run.options.testIds) && run.options.testIds.length);
        }
        if (Array.isArray(run.options.testIds) && run.options.testIds.length) return run.options.testIds.indexOf(definition.id) >= 0;
        if (Array.isArray(run.options.families) && run.options.families.length && !intersects(getDefinitionFamilies(definition), run.options.families)) return false;
        if (Array.isArray(run.options.categories) && run.options.categories.length && run.options.categories.indexOf(getDefinitionCategory(definition)) < 0) return false;
        if (!run.options.groups) return true;
        var selected = Array.isArray(run.options.groups) ? run.options.groups : [run.options.groups];
        return selected.indexOf(definition.group) >= 0;
    }

    /**************************************************************/

    /**
     * 
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     * @private
     * 
     */

    /**************************************************************/

    function waitForInterRequestDelay(run) {
        var delayMs = Math.max(0, Number(run.options.interRequestDelayMs) || 0);
        var elapsed = run.lastRequestStartedAt ? Date.now() - run.lastRequestStartedAt : delayMs;
        var remaining = Math.max(0, delayMs - elapsed);
        return new Promise(function (resolve) {
            if (!remaining || run.controller.signal.aborted) {
                if (!run.controller.signal.aborted) run.lastRequestStartedAt = Date.now();
                resolve();
                return;
            }
            var timeoutId = window.setTimeout(complete, remaining);
            function complete() {
                window.clearTimeout(timeoutId);
                run.controller.signal.removeEventListener('abort', complete);
                if (!run.controller.signal.aborted) run.lastRequestStartedAt = Date.now();
                resolve();
            }
            run.controller.signal.addEventListener('abort', complete, { once: true });
        });
    }

    function waitForPacing() {
        return Promise.resolve();
    }

    async function executeTest(run, definition) {
        var record = createRecord(definition);
        run.report.tests.push(record);
        getPanel().renderRecord(record);
        refreshReport(run);
        getPanel().renderRun(run);

        var fallbackRequirements = (definition.requires || []).filter(function (key) { return !!run.context[key + 'Fallback']; });
        if (fallbackRequirements.length && definition.id.indexOf('seed.') !== 0) {
            record.note = record.note ? record.note + ' fallback seed (harvest failed)' : 'fallback seed (harvest failed)';
        }
        var missing = (definition.requires || []).filter(function (key) { return !hasValue(run.context[key]); });
        if (missing.length) {
            settleRecord(run, record, 'skip', { skipReason: 'Missing seed: ' + missing.join(', ') + '.', assertions: [createAssertion('Prerequisites', 'skip', missing.join(', '))] });
            return record;
        }
        if (run.controller.signal.aborted) {
            settleRecord(run, record, 'skip', { skipReason: 'Run cancelled before request.', assertions: [createAssertion('Cancellation', 'skip', 'No request issued.')] });
            return record;
        }

        if (definition.when && !definition.when(run)) {
            settleRecord(run, record, 'skip', { skipReason: definition.skipReason || 'Not applicable to this profile.', assertions: [createAssertion('Profile applicability', 'skip', definition.skipReason || 'Not applicable.')] });
            return record;
        }

        if (definition.internal) {
            var internalResult = definition.internal(run, record);
            settleRecord(run, record, internalResult.outcome, internalResult);
            return record;
        }

        if (definition.run) {
            var customResult = await definition.run(run, definition, record);
            if (customResult.provides) Object.assign(run.context, customResult.provides);
            settleRecord(run, record, customResult.outcome, customResult);
            return record;
        }

        await waitForPacing(run, definition);
        var spec = typeof definition.request === 'function' ? definition.request(run.context, run) : definition.request;
        var response = await apiFetch(run, spec);
        var result = definition.evaluate ? definition.evaluate(response, run.context, run) : responseResult(response, definition.expect || { status: [200] });
        if (result.provides) Object.assign(run.context, result.provides);
        settleRecord(run, record, result.outcome, result);
        return record;
    }

    /**************************************************************/

    /**
     * 
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     * @private
     * 
     */

    /**************************************************************/

    function registerCleanup(run, name, action) {
        run.cleanupStack.push({ name: name, action: action });
    }

    async function runCleanup(run) {
        while (run.cleanupStack.length) {
            var cleanup = run.cleanupStack.pop();
            run.report.cleanup.attempted++;
            try {
                var result = await cleanup.action();
                var passed = result && (result.status === 200 || result.status === 204 || result.status === 404);
                run.report.cleanup.details.push({ name: cleanup.name, outcome: passed ? 'pass' : 'fail', status: result && result.status, detail: result && result.transportError });
                if (passed) run.report.cleanup.succeeded++; else run.report.cleanup.failed++;
            } catch (error) {
                run.report.cleanup.failed++;
                run.report.cleanup.details.push({ name: cleanup.name, outcome: 'fail', detail: String(error) });
            }
        }
    }
    /**************************************************************/
    /**
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     *
     * @private
     */
    /**************************************************************/
    function isFeatureDisabled(response) {
        return response && response.status === 503;
    }

    /**************************************************************/

    /**
     * 
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     * @private
     * 
     */

    /**************************************************************/

    function extractEncryptedId(item) {
        var direct = findValue(item, ['EncryptedId', 'EncryptedDocumentID', 'EncryptedSectionID', 'EncryptedProductID'], 1);
        if (hasValue(direct)) return direct;
        if (!item || typeof item !== 'object') return null;
        var key = Object.keys(item).find(function (candidate) { return /^encrypted.+id$/i.test(candidate); });
        return key ? item[key] : null;
    }

    /**************************************************************/

    /**
     * 
     * Coordinates endpoint-test execution, evidence recording, and cleanup.
     * @private
     * 
     */

    /**************************************************************/

    function arraySeed(response, harvest, featureAware) {
        if (featureAware && isFeatureDisabled(response)) {
            return {
                outcome: 'skip', response: response, skipReason: 'Feature is disabled (503); seed is unavailable.',
                assertions: [createAssertion('Feature gate', 'skip', 'Feature-disabled response is expected for this seed.')]
            };
        }
        var result = responseResult(response, { status: [200], jsonArray: true });
        if (result.outcome !== 'pass') return result;
        var item = firstItem(response.body);
        if (!item) {
            return {
                outcome: 'skip', response: response, skipReason: 'Seed endpoint returned no usable rows.',
                assertions: result.assertions.concat([createAssertion('Seed harvest', 'skip', 'The response array was empty.')])
            };
        }
        var provides = harvest(item, response.body) || {};
        var found = Object.keys(provides).filter(function (key) { return hasValue(provides[key]); });
        if (!found.length) {
            return {
                outcome: 'skip', response: response, skipReason: 'Response did not contain a usable seed value.',
                assertions: result.assertions.concat([createAssertion('Seed harvest', 'skip', 'No requested context value was found.')])
            };
        }
        result.provides = provides;
        result.assertions.push(createAssertion('Seed harvest', 'pass', 'Captured: ' + found.join(', ') + '.'));
        return result;
    }


    /**************************************************************/
    /**
     * Coordinates Phase 0 and Phase 1 execution through registered test definitions.
     *
     * @private
     */
    /**************************************************************/
    function getTest(id) {
        return TEST_REGISTRY.find(function (test) { return test.id === id; });
    }
    function getRegisteredOperationKeys() {
        return TEST_REGISTRY.filter(function (test) { return !!test.operationKey; }).map(function (test) {
            var split = test.operationKey.indexOf(' ');
            return normalizeOperationKey(test.operationKey.slice(0, split), test.operationKey.slice(split + 1));
        });
    }

    async function runPreflight(run) {
        var reachability = await executeTest(run, getTest('preflight.reachability'));
        if (reachability.outcome !== 'pass') return false;

        var auth = await executeTest(run, getTest('preflight.auth'));
        if (auth.outcome !== 'pass') return false;
        run.report.environment.authenticated = !!run.context.authenticated;
        run.report.environment.isAdmin = !!run.context.isAdmin;

        if (run.options.requireAnonymous && run.context.authenticated) {
            run.report.blocked = { code: 'safe-tests-require-anonymous', message: 'Safe API tests were not started because this browser has an authenticated session. Use a private window or run `/test api all` for baseline coverage.' };
            getPanel().renderRun(run, run.report.blocked.message);
            return false;
        }
        if (run.options.profile === 'authenticated' && !run.context.authenticated) {
            run.report.blocked = { code: 'authenticated-tests-require-login', message: 'Authenticated read coverage was not started because no authenticated session was detected.' };
            getPanel().renderRun(run, run.report.blocked.message);
            return false;
        }
        if (!run.options.runFullPreflight) return true;

        await executeTest(run, getTest('preflight.manifest'));
        await executeTest(run, getTest('preflight.openapi'));
        var features = await executeTest(run, getTest('preflight.features'));
        if (features.outcome === 'pass') run.report.environment.featureFlags = run.context.featureFlags;
        await executeTest(run, getTest('preflight.aiContext'));
        return true;
    }

    async function runRegisteredPhase(run, phase, afterCleanup) {
        var definitions = TEST_REGISTRY.filter(function (test) { return test.phase === phase && !!test.afterCleanup === !!afterCleanup && shouldRunTest(run, test); });
        for (var index = 0; index < definitions.length; index++) {
            if (run.controller.signal.aborted) break;
            var record = await executeTest(run, definitions[index]);
            if (record.outcome === 'fail' && run.options.stopOnFirstFail) break;
        }
    }

    function isPlannedForRun(run, definition) {
        if (definition.phase === 0) {
            return definition.id === 'preflight.reachability' || definition.id === 'preflight.auth' || !!run.options.runFullPreflight;
        }
        return shouldRunTest(run, definition);
    }

    function appendCancelledDefinitions(run) {
        var recordedIds = run.report.tests.reduce(function (ids, test) { ids[test.id] = true; return ids; }, {});
        TEST_REGISTRY.filter(function (definition) {
            return isPlannedForRun(run, definition) && !recordedIds[definition.id];
        }).forEach(function (definition) {
            var record = createRecord(definition);
            run.report.tests.push(record);
            settleRecord(run, record, 'skip', {
                skipReason: 'cancelled',
                assertions: [createAssertion('Cancellation', 'skip', 'No request was issued because the run was cancelled.')]
            });
        });
    }

    function finalizeOpenApiOutcome(run) {
        if (run.report.openApi.outcome !== 'pending') return;
        run.report.openApi.outcome = run.options.runFullPreflight && !run.report.environment.sameOrigin
            ? 'notObservable'
            : 'skipped';
    }

    async function runSeedDiscovery(run) {
        await runRegisteredPhase(run, 1);
    }

    /**************************************************************/

    /**
     * 
     * Coordinates Phase 0 and Phase 1 execution through registered test definitions.
     * @public
     * 
     */

    /**************************************************************/

    function cancelActiveRun() {
        if (!activeRun || activeRun.controller.signal.aborted) return;
        activeRun.report.cancelled = true;
        activeRun.controller.abort();
        getPanel().renderRun(activeRun, 'Cancellation requested; cleanup will still run.');
    }

    /**************************************************************/
    /**
     * Opens the diagnostic panel with a fixed chat-selected profile, without issuing requests.
     *
     * @param {Object} options Non-sensitive profile selection.
     * @returns {Object} Active on-demand panel.
     */
    /**************************************************************/
    function openApiPanel(options) {
        var normalized = getDefaultOptions(options);
        getPanel().configureFromCommand(normalized);
        getPanel().setSummary('Profile selected but blocked until the required confirmation(s) and fixture controls are supplied.');
        return getPanel().ensurePanel();
    }

    async function runApiTests(options) {
        if (activeRun && !activeRun.controller.signal.aborted) cancelActiveRun();
        var run = createRun(getDefaultOptions(options));
        activeRun = run;
        var currentPanel = getPanel().configureFromCommand(run.options);
        currentPanel.list.textContent = '';
        currentPanel.groups = {};
        if (run.options.includeImport && run.options.importFile) {
            getPanel().renderRun(run, 'Computing the selected durable import fixture SHA-256 before any request is issued.');
            try {
                run.report.fixtures.import.sha256 = await getImportFixtureSha256(run.options.importFile);
                if (!run.report.fixtures.import.sha256) run.report.findings.push('Import was not started because Web Crypto could not record the fixture SHA-256.');
            } catch (error) {
                run.report.findings.push('Import was not started because fixture SHA-256 computation failed: ' + String(error));
            }
        }
        getPanel().renderRun(run, 'Starting Phase 0 preflight and inventory evidence.');
        console.group('MedRecPro API Integration Tests');

        try {
            var canSeed = await runPreflight(run);
            if (canSeed) {
                getPanel().renderRun(run, 'Starting Phase 1 seed discovery.');
                await runSeedDiscovery(run);
                if (!run.controller.signal.aborted) {
                    getPanel().renderRun(run, 'Starting Phase 2 positive read coverage.');
                    await runRegisteredPhase(run, 2);
                }
                if (!run.controller.signal.aborted) {
                    getPanel().renderRun(run, 'Starting Phase 3 contract, negative, and auth-gate coverage.');
                    await runRegisteredPhase(run, 3);
                }
                if (!run.controller.signal.aborted) {
                    getPanel().renderRun(run, 'Starting selected Phase 4 opt-in coverage.');
                    await runRegisteredPhase(run, 4, false);
                }
            }
        } finally {
            await runCleanup(run);
            if (canSeed && !run.controller.signal.aborted) {
                await runRegisteredPhase(run, 4, true);
            }
            if (run.report.cancelled) appendCancelledDefinitions(run);
            finalizeOpenApiOutcome(run);
            run.report.completedAt = new Date().toISOString();
            refreshReport(run);
            getPanel().renderRun(run, run.report.cancelled ? 'Run cancelled; cleanup completed.' : run.report.blocked ? run.report.blocked.message : 'Phases 0-4 completed.');
            lastReport = run.report;
            if (activeRun === run) activeRun = null;
            console.groupEnd();
        }

        return run.report;
    }

    /**************************************************************/

    /**
     * 
     * Coordinates Phase 0 and Phase 1 execution through registered test definitions.
     * @public
     * 
     */

    /**************************************************************/

    function initializeQueryTrigger() {
        if (queryTriggerInitialized) return;
        queryTriggerInitialized = true;
        var query = new URLSearchParams(window.location.search);
        if (query.get('apitest') !== '1') return;
        var tarpit = query.get('tarpit') === 'disabled' ? 'disabled' : 'unknown';
        var queryOptions = {
            tarpitMode: tarpit,
            groups: query.get('group') ? [query.get('group')] : null,
            selection: { source: 'query', command: '?apitest=1', group: query.get('group') || null },
            requireAnonymous: true,
            runFullPreflight: true,
        };
        var currentPanel = getPanel().configureFromQuery(queryOptions);
        if (!isLoopbackHost()) {
            currentPanel.summary.textContent = 'Endpoint panel opened. Auto-run is blocked outside a loopback host.';
            return;
        }
        runApiTests(queryOptions);
    }

    return Object.freeze({ runApiTests: runApiTests, openApiPanel: openApiPanel, cancelActiveRun: cancelActiveRun, getLastReport: function () { return lastReport; }, isLocalDevelopment: isLocalDevelopment, initializeQueryTrigger: initializeQueryTrigger, auditedOperations: AUDITED_OPERATION_MANIFEST, defineTest: defineTest, createAssertion: createAssertion, normalizeOperationKey: normalizeOperationKey, apiFetch: apiFetch, responseResult: responseResult, isLoopbackHost: isLoopbackHost, arraySeed: arraySeed, findValue: findValue, firstItem: firstItem, hasValue: hasValue, expectStatus: expectStatus, expectJsonNumber: expectJsonNumber, expectFields: expectFields, expectObservableHeader: expectObservableHeader, expectPagedHeaders: expectPagedHeaders, expectContentType: expectContentType, expectXmlDocument: expectXmlDocument, expectAttachment: expectAttachment, expectRedirectProbe: expectRedirectProbe, extractEncryptedId: extractEncryptedId, isFeatureDisabled: isFeatureDisabled, registerCleanup: registerCleanup, waitForPacing: waitForPacing, hasRequiredConfirmation: hasRequiredConfirmation, getRegisteredOperationKeys: getRegisteredOperationKeys });
})(window.MedRecProApiTestManifest, window.MedRecProApiTestPanel);