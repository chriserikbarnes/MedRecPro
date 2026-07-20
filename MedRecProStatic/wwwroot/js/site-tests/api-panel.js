/**************************************************************/
/**
 * MedRecPro API Test Panel Module
 *
 * @fileoverview Renders the endpoint-diagnostic panel, queues DOM updates, and delegates run, cancel, and copy actions to the API runtime.
 *
 * @description Contains presentation concerns only; it has no manifest, transport, or phase-definition logic.
 *
 * @module site-tests/api-panel
 * @see MedRecProApiTestRuntime
 */
/**************************************************************/
window.MedRecProApiTestPanel = (function () {
    'use strict';
    /**************************************************************/
    /**
     * Creates one panel controller bound to runtime callbacks.
     *
     * @param {Object} callbacks Runtime callbacks for run state and safe report copying.
     * @returns {Object} Panel controller used by the API test runtime.
     */
    /**************************************************************/
    function create(callbacks) {
        var panel = null;
        /**************************************************************/
        /**
         * Supports the streaming endpoint-diagnostic panel without issuing requests itself.
         *
         * @private
         */
        /**************************************************************/
        function ensurePanel() {
            if (panel && document.body.contains(panel.root)) return panel;

            if (!document.getElementById('mrp-test-panel-style')) {
                var style = document.createElement('style');
                style.id = 'mrp-test-panel-style';
                style.textContent = '.mrp-test-panel{position:fixed;z-index:2147483647;top:1rem;right:1rem;width:min(31rem,calc(100vw - 2rem));max-height:calc(100vh - 2rem);display:flex;flex-direction:column;background:#18231f;color:#f5f1e7;border:1px solid #758a7b;border-radius:10px;box-shadow:0 18px 54px #0008;font:13px/1.4 system-ui,sans-serif}.mrp-test-panel header,.mrp-test-panel footer{padding:.75rem;border-bottom:1px solid #405448}.mrp-test-panel footer{border-top:1px solid #405448;border-bottom:0}.mrp-test-panel h2{font-size:1rem;margin:0}.mrp-test-actions{display:flex;gap:.4rem;flex-wrap:wrap;margin-top:.55rem}.mrp-test-panel button,.mrp-test-panel select,.mrp-test-panel input{font:inherit}.mrp-test-panel button{padding:.3rem .5rem;border:1px solid #758a7b;border-radius:4px;color:inherit;background:#2d4237;cursor:pointer}.mrp-test-panel button:hover{background:#3c5648}.mrp-test-meta,.mrp-test-options{padding:.55rem .75rem;background:#101813;color:#cbd8cb}.mrp-test-options{display:flex;align-items:center;gap:.45rem;flex-wrap:wrap}.mrp-test-options label{display:inline-flex;gap:.3rem;align-items:center}.mrp-test-profile{color:#cbd8cb}.mrp-test-confirmation,.mrp-test-disposable{display:flex;gap:.4rem;flex:1 0 100%;flex-wrap:wrap}.mrp-test-confirmation input,.mrp-test-disposable input:not([type="file"]){min-width:15rem;flex:1}.mrp-test-list{overflow:auto;overflow-x:hidden;min-width:0;padding:.5rem .75rem}.mrp-test-group{margin:.35rem 0;border:1px solid #405448;border-radius:5px;min-width:0}.mrp-test-group summary{cursor:pointer;padding:.35rem .5rem;font-weight:650}.mrp-test-row{padding:.35rem .5rem;border-top:1px solid #2c3b31;white-space:normal;min-width:0}.mrp-test-row[data-outcome="fail"]{background:#572a2a}.mrp-test-row[data-outcome="skip"]{background:#4b4530}.mrp-test-row details{margin-top:.2rem;color:#d7e2d5;min-width:0}.mrp-test-row pre{max-width:100%;box-sizing:border-box;white-space:pre-wrap;overflow-wrap:anywhere;word-break:break-word;overflow-x:auto}.mrp-test-status{display:inline-block;min-width:3.4rem;font-weight:700}.mrp-test-progress{height:4px;background:#405448}.mrp-test-progress span{display:block;height:100%;width:0;background:#e5a34a}.mrp-test-hidden{display:none!important}.mrp-test-counters{font-weight:700}';
                document.head.appendChild(style);
            }

            var root = document.createElement('aside');
            root.id = 'mrp-test-panel';
            root.className = 'mrp-test-panel';
            root.innerHTML = '<header><h2>MedRecPro endpoint diagnostic</h2><div class="mrp-test-actions"><button type="button" data-mrp-action="run">Run safe diagnostic</button><button type="button" data-mrp-action="abort">Abort</button><button type="button" data-mrp-action="close">Close</button></div></header><div class="mrp-test-meta"><div data-mrp="environment"></div><div class="mrp-test-progress"><span data-mrp="progress"></span></div><div class="mrp-test-counters" data-mrp="counters">PASS 0 | FAIL 0 | SKIP 0</div></div><div class="mrp-test-options"><span data-mrp="pacing">Pacing is determined by the target environment.</span><span class="mrp-test-profile" data-mrp="profile">Safe profile: read-only and no-cost.</span><div class="mrp-test-confirmation mrp-test-hidden" data-mrp="confirmation-controls"><input data-mrp="confirmation" placeholder="RUN CONFIRMED COST OR MUTATION"></div><div class="mrp-test-disposable mrp-test-hidden" data-mrp="disposable-controls"><input data-mrp="disposable-confirmation" placeholder="DISPOSABLE LOCAL DATABASE CONFIRMED"><input class="mrp-test-hidden" type="file" data-mrp="import-file" accept=".zip,application/zip"></div></div><div class="mrp-test-list" data-mrp="list"></div><footer><span data-mrp="summary">Panel opened; no requests issued.</span><div class="mrp-test-actions"><button type="button" data-mrp-filter="all">All</button><button type="button" data-mrp-filter="fail">Failed</button><button type="button" data-mrp-filter="skip">Skipped</button><button type="button" data-mrp-action="copy">Copy JSON report</button></div></footer>';
            var elapsed = document.createElement('div');
            elapsed.setAttribute('data-mrp', 'elapsed');
            elapsed.textContent = 'Elapsed 0:00';
            var header = root.querySelector('header');
            header.insertBefore(elapsed, header.querySelector('.mrp-test-actions'));
            root.querySelector('[data-mrp="counters"]').setAttribute('aria-live', 'polite');
            document.body.appendChild(root);

            panel = {
                root: root,
                environment: root.querySelector('[data-mrp="environment"]'),
                progress: root.querySelector('[data-mrp="progress"]'),
                counters: root.querySelector('[data-mrp="counters"]'),
                elapsed: root.querySelector('[data-mrp="elapsed"]'),
                summary: root.querySelector('[data-mrp="summary"]'),
                list: root.querySelector('[data-mrp="list"]'),
                pacing: root.querySelector('[data-mrp="pacing"]'),
                profile: root.querySelector('[data-mrp="profile"]'),
                confirmationControls: root.querySelector('[data-mrp="confirmation-controls"]'),
                confirmation: root.querySelector('[data-mrp="confirmation"]'),
                disposableControls: root.querySelector('[data-mrp="disposable-controls"]'),
                disposableConfirmation: root.querySelector('[data-mrp="disposable-confirmation"]'),
                importFile: root.querySelector('[data-mrp="import-file"]'),
                runButton: root.querySelector('[data-mrp-action="run"]'),
                commandOptions: {},
                filter: 'all',
                groups: {},
                queued: [],
                scheduled: false,
                elapsedTimer: null,
                elapsedRun: null
            };

            root.addEventListener('click', function (event) {
                var action = event.target.getAttribute('data-mrp-action');
                var filter = event.target.getAttribute('data-mrp-filter');
                if (filter) {
                    panel.filter = filter;
                    applyPanelFilter();
                    return;
                }
                if (action === 'close') root.remove();
                if (action === 'abort') callbacks.cancel();
                if (action === 'copy') copyLastReport();
                if (action === 'run') {
                    callbacks.run(Object.assign({}, panel.commandOptions, {
                        importFile: panel.importFile.files && panel.importFile.files[0] ? panel.importFile.files[0] : null,
                        confirmations: {
                            costOrMutation: panel.confirmation.value || null,
                            disposableData: panel.disposableConfirmation.value || null
                        }
                    }));
                }
            });

            return panel;
        }

        /**************************************************************/

        /**
         * 
         * Supports the streaming endpoint-diagnostic panel without issuing requests itself.
         * @private
         * 
         */

        /**************************************************************/

        function queuePanelUpdate(work) {
            var currentPanel = ensurePanel();
            currentPanel.queued.push(work);
            if (currentPanel.scheduled) return;
            currentPanel.scheduled = true;
            requestAnimationFrame(function () {
                currentPanel.queued.splice(0).forEach(function (item) { item(); });
                currentPanel.scheduled = false;
            });
        }

        /**************************************************************/

        /**
         * 
         * Supports the streaming endpoint-diagnostic panel without issuing requests itself.
         * @private
         * 
         */

        /**************************************************************/

        function applyPanelFilter() {
            if (!panel) return;
            Array.from(panel.list.querySelectorAll('.mrp-test-row')).forEach(function (row) {
                row.classList.toggle('mrp-test-hidden', panel.filter !== 'all' && row.dataset.outcome !== panel.filter);
            });
        }

        /**************************************************************/

        /**
         * 
         * Supports the streaming endpoint-diagnostic panel without issuing requests itself.
         * @private
         * 
         */

        /**************************************************************/

        function formatElapsed(milliseconds) {
            var totalSeconds = Math.max(0, Math.floor(milliseconds / 1000));
            return Math.floor(totalSeconds / 60) + ':' + String(totalSeconds % 60).padStart(2, '0');
        }

        function updateElapsed(run) {
            if (!panel || !panel.elapsed) return;
            if (panel.elapsedRun !== run) {
                if (panel.elapsedTimer) window.clearInterval(panel.elapsedTimer);
                panel.elapsedRun = run;
                panel.elapsedTimer = null;
            }
            var renderElapsed = function () {
                panel.elapsed.textContent = 'Elapsed ' + formatElapsed(Date.now() - new Date(run.report.startedAt).getTime());
            };
            renderElapsed();
            if (run.report.completedAt) {
                if (panel.elapsedTimer) window.clearInterval(panel.elapsedTimer);
                panel.elapsedTimer = null;
                return;
            }
            if (!panel.elapsedTimer) panel.elapsedTimer = window.setInterval(renderElapsed, 1000);
        }

        function renderRecord(record) {
            queuePanelUpdate(function () {
                var shouldAutoScroll = panel.list.scrollTop + panel.list.clientHeight >= panel.list.scrollHeight - 12;
                var group = panel.groups[record.group];
                if (!group) {
                    group = document.createElement('details');
                    group.className = 'mrp-test-group';
                    group.open = true;
                    var heading = document.createElement('summary');
                    heading.textContent = record.group;
                    group.appendChild(heading);
                    panel.groups[record.group] = group;
                    panel.list.appendChild(group);
                }

                var row = document.getElementById('mrp-test-row-' + record.id);
                if (!row) {
                    row = document.createElement('div');
                    row.id = 'mrp-test-row-' + record.id;
                    row.className = 'mrp-test-row';
                    group.appendChild(row);
                }
                row.dataset.outcome = record.outcome;
                row.textContent = '';
                var line = document.createElement('div');
                var chip = document.createElement('span');
                chip.className = 'mrp-test-status';
                chip.textContent = record.outcome === 'running' ? 'RUN' : record.outcome === 'pass' ? 'PASS' : record.outcome === 'fail' ? 'FAIL' : 'SKIP';
                line.appendChild(chip);
                var received = record.httpStatus === null ? (record.transportError || 'pending') : record.httpStatus;
                var expected = record.expectedStatus && record.expectedStatus.length ? ' | got ' + received + ' (expected ' + record.expectedStatus.join(' or ') + ')' : '';
                var duration = record.outcome === 'running' ? '' : ' | ' + record.durationMs + ' ms';
                line.appendChild(document.createTextNode((record.method ? record.method + ' ' : '') + (record.path || record.name) + ' - ' + record.name + duration + expected));
                row.appendChild(line);
                if (record.outcome !== 'running' && (record.assertions.length || record.skipReason || record.bodyExcerpt)) {
                    var details = document.createElement('details');
                    details.open = record.outcome === 'fail';
                    var detailSummary = document.createElement('summary');
                    detailSummary.textContent = record.skipReason || 'Assertion detail';
                    details.appendChild(detailSummary);
                    record.assertions.forEach(function (assertion) {
                        var item = document.createElement('div');
                        item.textContent = assertion.outcome.toUpperCase() + ' - ' + assertion.name + ': ' + assertion.detail;
                        details.appendChild(item);
                    });
                    if (record.bodyExcerpt) {
                        var response = document.createElement('pre');
                        response.textContent = record.bodyExcerpt;
                        details.appendChild(response);
                    }
                    row.appendChild(details);
                }
                applyPanelFilter();
                if (shouldAutoScroll) panel.list.scrollTop = panel.list.scrollHeight;
            });
        }

        /**************************************************************/

        /**
         * 
         * Supports the streaming endpoint-diagnostic panel without issuing requests itself.
         * 
         *
         * 
         * @private
         * 
         */

        /**************************************************************/

        function renderRun(run, message) {
            queuePanelUpdate(function () {
                updateElapsed(run);
                panel.environment.textContent = 'Page: ' + run.report.environment.pageOrigin + ' | API: ' + run.report.environment.apiBase + ' | ' + run.options.pacingLabel;
                var summary = run.report.summary;
                panel.counters.textContent = 'PASS ' + summary.passed + ' | FAIL ' + summary.failed + ' | SKIP ' + summary.skipped;
                panel.summary.textContent = message || ('Accounted ' + run.report.endpointCoverage.accounted + ' | invoked ' + run.report.endpointCoverage.invoked);
                var total = Math.max(summary.total, 1);
                panel.progress.style.width = Math.round(((summary.passed + summary.failed + summary.skipped) / total) * 100) + '%';
            });
        }

        /**************************************************************/

        /**
         * 
         * Supports the streaming endpoint-diagnostic panel without issuing requests itself.
         * 
         *
         * 
         * @private
         * 
         */

        /**************************************************************/

        function copyLastReport() {
            if (!callbacks.getLastReport()) {
                if (panel) panel.summary.textContent = 'No completed API report is available yet.';
                return;
            }
            var text = JSON.stringify(callbacks.getRedactedReport(), null, 2);
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(text).then(function () {
                    if (panel) panel.summary.textContent = 'Redacted JSON report copied.';
                }).catch(function () { showCopyFallback(text); });
            } else {
                showCopyFallback(text);
            }
        }

        /**************************************************************/

        /**
    
         * Supports the streaming endpoint-diagnostic panel without issuing requests itself.
         * 
         *
         * 
         * @private
         * 
         */

        /**************************************************************/

        function showCopyFallback(text) {
            var area = document.createElement('textarea');
            area.value = text;
            area.setAttribute('aria-label', 'Redacted API test report');
            if (panel) panel.root.appendChild(area);
            area.select();
            if (panel) panel.summary.textContent = 'Clipboard unavailable; select and copy the report text.';
        }

        /**************************************************************/
        /**
         * Clears prior rows and applies the options for a new diagnostic run.
         *
         * @param {Object} run Active API diagnostic run.
         * @returns {Object} Active panel state.
         */
        /**************************************************************/
        function reset(run) { var currentPanel = configureFromCommand(run.options); currentPanel.list.textContent = ''; currentPanel.groups = {}; return currentPanel; }
        /**************************************************************/
        /**
         * Applies non-sensitive query-string UI selections without starting a run.
         *
         * @param {Object} options Query-derived panel options.
         * @returns {Object} Active panel state.
         */
        /**************************************************************/
        function configureFromQuery(options) { return configureFromCommand(options || {}); }
        /**************************************************************/
        /**
         * Shows only the safeguards required by the selected profile.
         *
         * @param {Object} currentPanel Active panel state.
         * @param {Object} options Fixed command options selected by the chat router.
         */
        /**************************************************************/
        function configureProfileControls(currentPanel, options) { var requiresConfirmation = !!(options.includeAi || options.includeMutating || options.includeCacheClear || options.includeAdminWrites || options.includeImport || options.includeSlow || options.includeLogout); var requiresDisposable = !!(options.includeAdminWrites || options.includeImport); var profile = options.profile || 'anonymous'; currentPanel.profile.textContent = requiresConfirmation ? 'Selected profile: ' + profile + '.' : profile === 'all' ? 'All baseline profile: non-destructive coverage.' : options.requireAnonymous ? 'Safe profile: anonymous session required.' : 'Safe profile: read-only and no-cost.'; currentPanel.confirmationControls.classList.toggle('mrp-test-hidden', !requiresConfirmation); currentPanel.disposableControls.classList.toggle('mrp-test-hidden', !requiresDisposable); currentPanel.importFile.classList.toggle('mrp-test-hidden', !options.includeImport); currentPanel.runButton.textContent = requiresConfirmation ? 'Run selected profile' : profile === 'all' ? 'Run all baseline' : 'Run safe diagnostic'; }

        /**************************************************************/
        /**
         * Preselects a chat-command profile without issuing a request or retaining confirmation text.
         *
         * @param {Object} options Fixed command options selected by the chat router.
         * @returns {Object} Active panel state.
         */
        /**************************************************************/
        function configureFromCommand(options) { var currentPanel = ensurePanel(); var selectedOptions = options || {}; currentPanel.commandOptions = Object.assign({}, selectedOptions); currentPanel.pacing.textContent = selectedOptions.pacingLabel || 'Pacing is determined when the run starts.'; configureProfileControls(currentPanel, selectedOptions); return currentPanel; }

        /**************************************************************/
        /**
         * Displays a non-request informational message in the panel footer.
         *
         * @param {string} message Message to display.
         */
        /**************************************************************/
        function setSummary(message) { ensurePanel().summary.textContent = message; }
        return Object.freeze({ ensurePanel: ensurePanel, reset: reset, configureFromQuery: configureFromQuery, configureFromCommand: configureFromCommand, setSummary: setSummary, renderRecord: renderRecord, renderRun: renderRun });
    }
    return Object.freeze({ create: create });
})();