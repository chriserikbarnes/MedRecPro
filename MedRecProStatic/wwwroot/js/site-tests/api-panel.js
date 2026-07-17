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
window.MedRecProApiTestPanel=(function(){
    'use strict';
    /**************************************************************/
    /**
     * Creates one panel controller bound to runtime callbacks.
     *
     * @param {Object} callbacks Runtime callbacks for run state and safe report copying.
     * @returns {Object} Panel controller used by the API test runtime.
     */
    /**************************************************************/
    function create(callbacks){
        var panel=null;
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
            style.textContent = '.mrp-test-panel{position:fixed;z-index:2147483647;top:1rem;right:1rem;width:min(31rem,calc(100vw - 2rem));max-height:calc(100vh - 2rem);display:flex;flex-direction:column;background:#18231f;color:#f5f1e7;border:1px solid #758a7b;border-radius:10px;box-shadow:0 18px 54px #0008;font:13px/1.4 system-ui,sans-serif}.mrp-test-panel header,.mrp-test-panel footer{padding:.75rem;border-bottom:1px solid #405448}.mrp-test-panel footer{border-top:1px solid #405448;border-bottom:0}.mrp-test-panel h2{font-size:1rem;margin:0}.mrp-test-actions{display:flex;gap:.4rem;flex-wrap:wrap;margin-top:.55rem}.mrp-test-panel button,.mrp-test-panel select,.mrp-test-panel input{font:inherit}.mrp-test-panel button{padding:.3rem .5rem;border:1px solid #758a7b;border-radius:4px;color:inherit;background:#2d4237;cursor:pointer}.mrp-test-panel button:hover{background:#3c5648}.mrp-test-meta,.mrp-test-options{padding:.55rem .75rem;background:#101813;color:#cbd8cb}.mrp-test-options label{display:inline-flex;gap:.3rem;align-items:center;margin:.15rem .5rem .15rem 0}.mrp-test-list{overflow:auto;padding:.5rem .75rem}.mrp-test-group{margin:.35rem 0;border:1px solid #405448;border-radius:5px}.mrp-test-group summary{cursor:pointer;padding:.35rem .5rem;font-weight:650}.mrp-test-row{padding:.35rem .5rem;border-top:1px solid #2c3b31;white-space:normal}.mrp-test-row[data-outcome="fail"]{background:#572a2a}.mrp-test-row[data-outcome="skip"]{background:#4b4530}.mrp-test-row details{margin-top:.2rem;color:#d7e2d5}.mrp-test-status{display:inline-block;min-width:3.4rem;font-weight:700}.mrp-test-progress{height:4px;background:#405448}.mrp-test-progress span{display:block;height:100%;width:0;background:#e5a34a}.mrp-test-hidden{display:none}.mrp-test-counters{font-weight:700}';
            document.head.appendChild(style);
        }

        var root = document.createElement('aside');
        root.id = 'mrp-test-panel';
        root.className = 'mrp-test-panel';
        root.innerHTML = '<header><h2>MedRecPro endpoint diagnostic</h2><div class="mrp-test-actions"><button type="button" data-mrp-action="run">Run selected profile</button><button type="button" data-mrp-action="abort">Abort</button><button type="button" data-mrp-action="close">Close</button></div></header><div class="mrp-test-meta"><div data-mrp="environment"></div><div class="mrp-test-progress"><span data-mrp="progress"></span></div><div class="mrp-test-counters" data-mrp="counters">PASS 0 ? FAIL 0 ? SKIP 0</div></div><div class="mrp-test-options"><label>Tarpit <select data-mrp="tarpit"><option value="unknown">unknown / conservative</option><option value="active">active / conservative</option><option value="disabled">disabled (operator attested)</option></select></label><label><input type="checkbox" data-mrp="conversation" checked> conversation lifecycle</label><label><input type="checkbox" data-mrp="ai"> paid AI (Phase 4)</label><label><input type="checkbox" data-mrp="mutating"> mutation (Phase 4)</label><input data-mrp="confirmation" placeholder="confirmation required in Phase 4"></div><div class="mrp-test-list" data-mrp="list"></div><footer><span data-mrp="summary">Panel opened; no requests issued.</span><div class="mrp-test-actions"><button type="button" data-mrp-filter="all">All</button><button type="button" data-mrp-filter="fail">Failed</button><button type="button" data-mrp-filter="skip">Skipped</button><button type="button" data-mrp-action="copy">Copy JSON report</button></div></footer>';
        document.body.appendChild(root);
        root.querySelector('.mrp-test-options').insertAdjacentHTML('beforeend', '<label><input type="checkbox" data-mrp="cache-clear"> clear managed cache (non-reverting)</label><label><input type="checkbox" data-mrp="admin"> disposable admin</label><label><input type="checkbox" data-mrp="import"> durable import</label><label><input type="checkbox" data-mrp="slow"> slow comparison</label><label><input type="checkbox" data-mrp="logout"> logout after cleanup</label><input data-mrp="disposable-confirmation" placeholder="DISPOSABLE LOCAL DATABASE CONFIRMED"><input type="file" data-mrp="import-file" accept=".zip,application/zip">');

        panel = {
            root: root,
            environment: root.querySelector('[data-mrp="environment"]'),
            progress: root.querySelector('[data-mrp="progress"]'),
            counters: root.querySelector('[data-mrp="counters"]'),
            summary: root.querySelector('[data-mrp="summary"]'),
            list: root.querySelector('[data-mrp="list"]'),
            tarpit: root.querySelector('[data-mrp="tarpit"]'),
            conversation: root.querySelector('[data-mrp="conversation"]'),
            ai: root.querySelector('[data-mrp="ai"]'),
            mutating: root.querySelector('[data-mrp="mutating"]'),
            cacheClear: root.querySelector('[data-mrp="cache-clear"]'),
            admin: root.querySelector('[data-mrp="admin"]'),
            import: root.querySelector('[data-mrp="import"]'),
            slow: root.querySelector('[data-mrp="slow"]'),
            logout: root.querySelector('[data-mrp="logout"]'),
            importFile: root.querySelector('[data-mrp="import-file"]'),
            disposableConfirmation: root.querySelector('[data-mrp="disposable-confirmation"]'),
            commandOptions: {},
            filter: 'all',
            groups: {},
            queued: [],
            scheduled: false
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
                    tarpitMode: panel.tarpit.value,
                    conversationLifecycle: panel.conversation.checked,
                    includeAi: panel.ai.checked,
                    includeMutating: panel.mutating.checked,
                    includeCacheClear: panel.cacheClear.checked,
                    includeAdminWrites: panel.admin.checked,
                    includeImport: panel.import.checked,
                    includeSlow: panel.slow.checked,
                    includeLogout: panel.logout.checked,
                    importFile: panel.importFile.files && panel.importFile.files[0] ? panel.importFile.files[0] : null,
                    confirmations: {
                        costOrMutation: root.querySelector('[data-mrp="confirmation"]').value || null,
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
     * 
     *
     * 
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
     * 
     *
     * 
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
     * 
     *
     * 
     * @private
     * 
     */

    /**************************************************************/

    function renderRecord(record) {
        queuePanelUpdate(function () {
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
            chip.textContent = record.outcome === 'running' ? '? RUN' : record.outcome === 'pass' ? '? PASS' : record.outcome === 'fail' ? '? FAIL' : '? SKIP';
            line.appendChild(chip);
            line.appendChild(document.createTextNode((record.method ? record.method + ' ' : '') + (record.path || record.name) + ' ? ' + record.name + (record.httpStatus ? ' (' + record.httpStatus + ')' : '')));
            row.appendChild(line);
            if (record.outcome !== 'running' && (record.assertions.length || record.skipReason || record.bodyExcerpt)) {
                var details = document.createElement('details');
                details.open = record.outcome === 'fail';
                var detailSummary = document.createElement('summary');
                detailSummary.textContent = record.skipReason || 'Assertion detail';
                details.appendChild(detailSummary);
                record.assertions.forEach(function (assertion) {
                    var item = document.createElement('div');
                    item.textContent = assertion.outcome.toUpperCase() + ' ? ' + assertion.name + ': ' + assertion.detail;
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
            panel.environment.textContent = 'Page: ' + run.report.environment.pageOrigin + ' | API: ' + run.report.environment.apiBase + ' | tarpit: ' + run.options.tarpitMode;
            var summary = run.report.summary;
            panel.counters.textContent = 'PASS ' + summary.passed + ' ? FAIL ' + summary.failed + ' ? SKIP ' + summary.skipped;
            panel.summary.textContent = message || ('Accounted ' + run.report.endpointCoverage.accounted + ' ? invoked ' + run.report.endpointCoverage.invoked);
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
    function reset(run){var currentPanel=ensurePanel();currentPanel.list.textContent='';currentPanel.groups={};currentPanel.tarpit.value=run.options.tarpitMode;currentPanel.conversation.checked=run.options.conversationLifecycle;return currentPanel;}
    /**************************************************************/
    /**
     * Applies non-sensitive query-string UI selections without starting a run.
     *
     * @param {Object} options Query-derived panel options.
     */
    /**************************************************************/
    function configureFromQuery(options){var currentPanel=ensurePanel();currentPanel.tarpit.value=options.tarpitMode;currentPanel.ai.checked=!!options.includeAi;currentPanel.mutating.checked=!!options.includeMutating;}
    /**************************************************************/
    /**
     * Preselects a chat-command profile without issuing a request or retaining confirmation text.
     *
     * @param {Object} options Fixed command options selected by the chat router.
     */
    /**************************************************************/
    function configureFromCommand(options){var currentPanel=ensurePanel();currentPanel.commandOptions=Object.assign({},options || {});currentPanel.tarpit.value=options.tarpitMode || currentPanel.tarpit.value;currentPanel.ai.checked=!!options.includeAi;currentPanel.mutating.checked=!!options.includeMutating;currentPanel.cacheClear.checked=!!options.includeCacheClear;currentPanel.admin.checked=!!options.includeAdminWrites;currentPanel.import.checked=!!options.includeImport;currentPanel.slow.checked=!!options.includeSlow;currentPanel.logout.checked=!!options.includeLogout;}    /**************************************************************/
    /**
     * Displays a non-request informational message in the panel footer.
     *
     * @param {string} message Message to display.
     */
    /**************************************************************/
    function setSummary(message){ensurePanel().summary.textContent=message;}
    return Object.freeze({ensurePanel:ensurePanel,reset:reset,configureFromQuery:configureFromQuery,configureFromCommand:configureFromCommand,setSummary:setSummary,renderRecord:renderRecord,renderRun:renderRun});
    }
    return Object.freeze({create:create});
})();