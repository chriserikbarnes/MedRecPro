/**************************************************************/
/**
 * MedRecPro API Test Phase 0–1 Definitions
 *
 * @fileoverview Registers browser-origin preflight and seed-discovery definitions with the shared API test runtime.
 *
 * @description Keeps phase-specific endpoint expectations separate from manifest, UI, and execution mechanics.
 *
 * @module site-tests/api-phases
 * @see MedRecProApiTestRuntime
 */
/**************************************************************/
(function(runtime){
    'use strict';
    if(!runtime){throw new Error('MedRecPro API test runtime must load before Phase 0–1 definitions.');}
    var auditedOperations=runtime.auditedOperations;
    var defineTest=runtime.defineTest;
    var createAssertion=runtime.createAssertion;
    var normalizeOperationKey=runtime.normalizeOperationKey;
    var apiFetch=runtime.apiFetch;
    var responseResult=runtime.responseResult;
    var isLoopbackHost=runtime.isLoopbackHost;
    var arraySeed=runtime.arraySeed;
    var findValue=runtime.findValue;
    var firstItem=runtime.firstItem;
    var hasValue=runtime.hasValue;
    var expectStatus=runtime.expectStatus;
    var registerCleanup=runtime.registerCleanup;
    var waitForPacing=runtime.waitForPacing;

    /**************************************************************/
    /**
     * Registers Phase 0 preflight and Phase 1 seed-discovery endpoint definitions.
     *
     * @description The shared runtime executes these definitions only after bootstrap completes dependency loading.
     */
    /**************************************************************/
    defineTest({
        id: 'preflight.reachability', phase: 0, group: 'Preflight', evidenceKind: 'positive',
        operationKey: 'GET /api/Settings/info', name: 'API reachability', method: 'GET', path: '/api/settings/info', expectedStatus: [200],
        request: { method: 'GET', path: '/api/settings/info' },
        evaluate: function (response) { return responseResult(response, { status: [200], jsonObject: true }); }
    });

    defineTest({
        id: 'preflight.manifest', phase: 0, group: 'Preflight', evidenceKind: 'positive',
        name: 'Audited 120-operation manifest is fully mapped by definitions', internal: function () {
            var keys = auditedOperations.map(function (entry) {
                var split = entry.indexOf(' ');
                return normalizeOperationKey(entry.slice(0, split), entry.slice(split + 1));
            });
            var unique = new Set(keys);
            var registered = runtime.getRegisteredOperationKeys();
            var missing = keys.filter(function (key) { return registered.indexOf(key) < 0; });
            var stale = registered.filter(function (key) { return keys.indexOf(key) < 0; });
            var passed = auditedOperations.length === 120 && unique.size === 120 && !missing.length && !stale.length;
            return {
                outcome: passed ? 'pass' : 'fail', positiveContractVerified: passed,
                assertions: [
                    createAssertion('Audited operation inventory', auditedOperations.length === 120 && unique.size === 120 ? 'pass' : 'fail', 'Found ' + auditedOperations.length + ' entries and ' + unique.size + ' unique normalized keys.'),
                    createAssertion('Definition-to-manifest coverage', !missing.length && !stale.length ? 'pass' : 'fail', 'Missing definitions: ' + missing.length + '; stale definitions: ' + stale.length + '.')
                ]
            };
        }
    });

    defineTest({
        id: 'preflight.openapi', phase: 0, group: 'Preflight', evidenceKind: 'positive',
        name: 'Live OpenAPI manifest comparison', method: 'GET', path: '/swagger/v1/swagger.json',
        run: async function (run) {
            var openApi = run.report.openApi;
            openApi.url = (run.options.apiBase || window.location.origin) + '/swagger/v1/swagger.json';
            if (!run.report.environment.sameOrigin) {
                openApi.outcome = 'notObservable';
                return {
                    outcome: 'skip', skipReason: 'Live Swagger is not CORS-readable from this cross-origin page.',
                    assertions: [createAssertion('Live OpenAPI comparison', 'notObservable', 'Skipped doomed cross-origin Swagger fetch.')]
                };
            }
            var response = await apiFetch(run, { method: 'GET', path: '/swagger/v1/swagger.json', credentials: 'include' });
            var basic = responseResult(response, { status: [200], jsonObject: true });
            if (basic.outcome !== 'pass') {
                openApi.outcome = 'failed';
                return basic;
            }
            var paths = response.body.paths || {};
            var discovered = [];
            Object.keys(paths).forEach(function (path) {
                Object.keys(paths[path] || {}).forEach(function (method) {
                    if (/^(get|post|put|delete)$/i.test(method)) discovered.push(normalizeOperationKey(method, path));
                });
            });
            var registered = auditedOperations.map(function (entry) {
                var split = entry.indexOf(' ');
                return normalizeOperationKey(entry.slice(0, split), entry.slice(split + 1));
            });
            openApi.discovered = discovered.slice().sort();
            openApi.missingFromRegistry = discovered.filter(function (key) { return registered.indexOf(key) < 0; }).sort();
            openApi.staleRegistry = registered.filter(function (key) { return discovered.indexOf(key) < 0; }).sort();
            var passed = !openApi.missingFromRegistry.length && !openApi.staleRegistry.length;
            openApi.outcome = passed ? 'pass' : 'failed';
            return {
                outcome: passed ? 'pass' : 'fail', response: response, positiveContractVerified: passed,
                assertions: basic.assertions.concat([createAssertion('Live OpenAPI comparison', passed ? 'pass' : 'fail', passed ? 'Live Swagger matches the audited registry.' : 'Missing from registry: ' + openApi.missingFromRegistry.length + '; stale registry: ' + openApi.staleRegistry.length + '.')])
            };
        }
    });

    defineTest({
        id: 'preflight.features', phase: 0, group: 'Preflight', evidenceKind: 'positive',
        operationKey: 'GET /api/Settings/features', name: 'Feature flags are available', method: 'GET', path: '/api/settings/features', expectedStatus: [200],
        request: { method: 'GET', path: '/api/settings/features' },
        evaluate: function (response) {
            var result = responseResult(response, { status: [200], jsonObject: true });
            if (result.outcome === 'pass') {
                result.provides = { featureFlags: response.body };
                result.assertions.push(createAssertion('Feature adaptation', 'pass', 'Feature flags captured for later expectations.'));
            }
            return result;
        }
    });

    defineTest({
        id: 'preflight.auth', phase: 0, group: 'Preflight', evidenceKind: 'authGate',
        operationKey: 'GET /api/Auth/user', name: 'Authentication profile is discovered', method: 'GET', path: '/api/auth/user', expectedStatus: [401, 200],
        request: { method: 'GET', path: '/api/auth/user' },
        evaluate: function (response) {
            var status = expectStatus(response, [401, 200]);
            if (status.outcome === 'fail') return { outcome: 'fail', response: response, assertions: [status] };
            var authenticated = response.status === 200;
            var serialized = authenticated ? JSON.stringify(response.body || {}) : '';
            var isAdmin = authenticated && /\badmin\b/i.test(serialized);
            return {
                outcome: 'pass', response: response, provides: { authenticated: authenticated, isAdmin: isAdmin },
                assertions: [status, createAssertion('Authentication state', 'pass', authenticated ? 'Authenticated session detected.' : 'Verified anonymous (401).')]
            };
        }
    });

    defineTest({
        id: 'preflight.aiContext', phase: 0, group: 'Preflight', evidenceKind: 'positive',
        operationKey: 'GET /api/Ai/context', name: 'AI context and demo mode are available', method: 'GET', path: '/api/ai/context', expectedStatus: [200],
        request: { method: 'GET', path: '/api/ai/context' },
        evaluate: function (response) {
            var result = responseResult(response, { status: [200], jsonObject: true });
            if (result.outcome === 'pass') result.provides = { aiContext: response.body };
            return result;
        }
    });

    defineTest({
        id: 'seed.label.productLatest', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/Label/product/latest', name: 'Harvest latest label document, UNII, and product name', method: 'GET', path: '/api/label/product/latest', expectedStatus: [200],
        request: { method: 'GET', path: '/api/label/product/latest', query: { pageNumber: 1, pageSize: 1 } },
        evaluate: function (response) {
            return arraySeed(response, function (item) {
                return {
                    labelDocumentGuid: findValue(item, ['DocumentGUID', 'DocumentGuid'], 3),
                    unii: findValue(item, ['UNII'], 3),
                    productName: findValue(item, ['ProductName'], 3)
                };
            });
        }
    });

    defineTest({
        id: 'seed.label.navigation', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/Label/document/navigation', name: 'Harvest document and set GUIDs', method: 'GET', path: '/api/label/document/navigation', expectedStatus: [200],
        request: { method: 'GET', path: '/api/label/document/navigation', query: { latestOnly: true, pageNumber: 1, pageSize: 1 } },
        evaluate: function (response) {
            return arraySeed(response, function (item) {
                return {
                    navDocumentGuid: findValue(item, ['DocumentGUID', 'DocumentGuid'], 2),
                    setGuid: findValue(item, ['SetGUID', 'SetGuid'], 2)
                };
            });
        }
    });

    defineTest({
        id: 'seed.ae.catalog', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/AdverseEvent/products/catalog', name: 'Harvest two AE document GUIDs', method: 'GET', path: '/api/adverseevent/products/catalog', expectedStatus: [200],
        request: { method: 'GET', path: '/api/adverseevent/products/catalog', query: { pageNumber: 1, pageSize: 2 } },
        evaluate: function (response) {
            if (isFeatureDisabled(response)) return arraySeed(response, function () { return {}; }, true);
            var result = responseResult(response, { status: [200], jsonArray: true });
            if (result.outcome !== 'pass') return result;
            var rows = Array.isArray(response.body) ? response.body : [];
            var first = rows[0] || {};
            var second = rows[1] || {};
            var firstGuid = findValue(first, ['DocumentGUID', 'DocumentGuid'], 2);
            var secondGuid = findValue(second, ['DocumentGUID', 'DocumentGuid'], 2);
            if (!hasValue(firstGuid)) {
                return { outcome: 'skip', response: response, skipReason: 'AE catalog did not contain an AE document.', assertions: result.assertions.concat([createAssertion('Seed harvest', 'skip', 'No document GUID was available.')]) };
            }
            result.provides = { aeDocumentGuidA: firstGuid, aeDocumentGuidB: secondGuid || null };
            result.assertions.push(createAssertion('Seed harvest', 'pass', secondGuid ? 'Captured two AE document GUIDs.' : 'Captured one AE document GUID; a pair remains unavailable.'));
            return result;
        }
    });

    defineTest({
        id: 'seed.ae.classes', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/AdverseEvent/correlation/classes', name: 'Harvest a pharmacologic class code', method: 'GET', path: '/api/adverseevent/correlation/classes', expectedStatus: [200],
        request: { method: 'GET', path: '/api/adverseevent/correlation/classes', query: { pageNumber: 1, pageSize: 1 } },
        evaluate: function (response) {
            return arraySeed(response, function (item) {
                return { pharmClassCode: findValue(item, ['PharmacologicClassCode', 'ClassCode', 'Code', 'PharmacologicClass'], 2) };
            }, true);
        }
    });

    defineTest({
        id: 'seed.ae.systems', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/AdverseEvent/correlation/systems', name: 'Harvest a MedDRA system name', method: 'GET', path: '/api/adverseevent/correlation/systems', expectedStatus: [200],
        request: { method: 'GET', path: '/api/adverseevent/correlation/systems', query: { pageNumber: 1, pageSize: 1 } },
        evaluate: function (response) {
            return arraySeed(response, function (item) {
                return { systemName: findValue(item, ['SystemName', 'ParameterCategory', 'Name'], 2) };
            }, true);
        }
    });

    defineTest({
        id: 'seed.label.sectionMenu', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/Label/sectionMenu', name: 'Harvest a read-safe section menu selection', method: 'GET', path: '/api/label/sectionMenu', expectedStatus: [200],
        request: { method: 'GET', path: '/api/label/sectionMenu' },
        evaluate: function (response) {
            var result = responseResult(response, { status: [200], jsonArray: true });
            if (result.outcome !== 'pass') return result;
            var values = Array.isArray(response.body) ? response.body.filter(function (value) { return typeof value === 'string' && value.trim(); }) : [];
            var menuSelection = values.find(function (value) { return value.toLowerCase() === 'document'; }) || values[0];
            if (!menuSelection) return { outcome: 'skip', response: response, skipReason: 'No supported section menu entries were returned.', assertions: result.assertions.concat([createAssertion('Seed harvest', 'skip', 'Section menu was empty.')]) };
            result.provides = { menuSelection: menuSelection };
            result.assertions.push(createAssertion('Seed harvest', 'pass', 'Captured a read-safe section selection.'));
            return result;
        }
    });

    defineTest({
        id: 'seed.label.sectionRecord', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/Label/section/{menuSelection}', name: 'Harvest an encrypted section ID', method: 'GET', path: '/api/label/section/{menuSelection}', expectedStatus: [200], requires: ['menuSelection'],
        request: function (context) { return { method: 'GET', path: '/api/label/section/' + encodeURIComponent(context.menuSelection), query: { pageNumber: 1, pageSize: 1 } }; },
        evaluate: function (response) {
            return arraySeed(response, function (item) { return { encryptedId: extractEncryptedId(item) }; });
        }
    });

    defineTest({
        id: 'seed.label.sectionSummaries', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/Label/section/summaries', name: 'Harvest a section code', method: 'GET', path: '/api/label/section/summaries', expectedStatus: [200],
        request: { method: 'GET', path: '/api/label/section/summaries', query: { pageNumber: 1, pageSize: 1 } },
        evaluate: function (response) { return arraySeed(response, function (item) { return { sectionCode: findValue(item, ['SectionCode'], 2) }; }); }
    });

    defineTest({
        id: 'seed.label.applicationSummaries', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/Label/application-number/summaries', name: 'Harvest an application number', method: 'GET', path: '/api/label/application-number/summaries', expectedStatus: [200],
        request: { method: 'GET', path: '/api/label/application-number/summaries', query: { pageNumber: 1, pageSize: 1 } },
        evaluate: function (response) { return arraySeed(response, function (item) { return { applicationNumber: findValue(item, ['ApplicationNumber'], 2) }; }); }
    });

    defineTest({
        id: 'seed.label.labelerSummaries', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/Label/labeler/summaries', name: 'Harvest a labeler name', method: 'GET', path: '/api/label/labeler/summaries', expectedStatus: [200],
        request: { method: 'GET', path: '/api/label/labeler/summaries', query: { pageNumber: 1, pageSize: 1 } },
        evaluate: function (response) { return arraySeed(response, function (item) { return { labelerName: findValue(item, ['LabelerName'], 2) }; }); }
    });

    defineTest({
        id: 'seed.label.ingredientSummaries', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/Label/ingredient/summaries', name: 'Harvest an ingredient substance name', method: 'GET', path: '/api/label/ingredient/summaries', expectedStatus: [200],
        request: { method: 'GET', path: '/api/label/ingredient/summaries', query: { pageNumber: 1, pageSize: 1 } },
        evaluate: function (response) { return arraySeed(response, function (item) { return { substanceName: findValue(item, ['SubstanceName'], 2) }; }); }
    });

    defineTest({
        id: 'seed.label.ndcPrefix', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'GET /api/Label/ndc/search', name: 'Harvest an NDC product code with a bounded prefix probe', method: 'GET', path: '/api/label/ndc/search', expectedStatus: [200],
        run: async function (run, definition) {
            var assertions = [];
            for (var index = 0; index < 10; index++) {
                await waitForPacing(run, definition);
                var response = await apiFetch(run, { method: 'GET', path: '/api/label/ndc/search', query: { productCode: String(index), pageNumber: 1, pageSize: 1 } });
                if (response.transportError) return responseResult(response, { status: [200], jsonArray: true });
                var status = expectStatus(response, [200]);
                assertions.push(createAssertion('NDC prefix ' + index, status.outcome, status.detail));
                if (status.outcome === 'fail') return { outcome: 'fail', response: response, assertions: assertions };
                var productCode = findValue(firstItem(response.body), ['ProductCode', 'Ndc', 'NDC'], 2);
                if (hasValue(productCode)) {
                    return { outcome: 'pass', response: response, provides: { productCode: productCode }, positiveContractVerified: true, assertions: assertions.concat([createAssertion('Seed harvest', 'pass', 'Captured a product code from prefix ' + index + '.')]) };
                }
            }
            return { outcome: 'skip', skipReason: 'No NDC product code was found in the ten bounded prefix probes.', assertions: assertions.concat([createAssertion('Seed harvest', 'skip', 'No NDC prefix produced a result.')]) };
        }
    });

    defineTest({
        id: 'seed.ai.conversation', phase: 1, group: 'Seed discovery', evidenceKind: 'positive',
        operationKey: 'POST /api/Ai/conversations', name: 'Create and register an in-memory conversation for cleanup', method: 'POST', path: '/api/ai/conversations', expectedStatus: [200],
        when: function (run) { return isLoopbackHost() && run.options.conversationLifecycle; }, skipReason: 'Conversation lifecycle is loopback-only and was not enabled.',
        run: async function (run, definition) {
            await waitForPacing(run, definition);
            var response = await apiFetch(run, { method: 'POST', path: '/api/ai/conversations' });
            var result = responseResult(response, { status: [200], jsonObject: true });
            if (result.outcome !== 'pass') return result;
            var conversationId = findValue(response.body, ['ConversationId', 'conversationId'], 2);
            if (!hasValue(conversationId)) {
                return { outcome: 'skip', response: response, skipReason: 'Conversation response did not provide an ID.', assertions: result.assertions.concat([createAssertion('Seed harvest', 'skip', 'ConversationId was missing.')]) };
            }
            registerCleanup(run, 'Delete seed conversation', function () {
                return apiFetch({ options: Object.assign({}, run.options, { requestTimeoutMs: Math.min(run.options.requestTimeoutMs, 10000) }), controller: new AbortController() }, { method: 'DELETE', path: '/api/ai/conversations/' + encodeURIComponent(conversationId) });
            });
            result.provides = { conversationId: conversationId };
            result.assertions.push(createAssertion('Cleanup registration', 'pass', 'Conversation cleanup was registered immediately.'));
            return result;
        }
    });

    /**************************************************************/
    /**
     * Defines shared Phase 2 and Phase 3 contract helpers.
     *
     * @remarks
     * The helpers preserve one literal operation key per registered definition so the host-side
     * Swagger inventory verifier can audit this module without invoking browser code.
     */
    /**************************************************************/
    var expectJsonNumber=runtime.expectJsonNumber;
    var expectFields=runtime.expectFields;
    var expectObservableHeader=runtime.expectObservableHeader;
    var expectPagedHeaders=runtime.expectPagedHeaders;
    var expectContentType=runtime.expectContentType;
    var expectXmlDocument=runtime.expectXmlDocument;
    var expectAttachment=runtime.expectAttachment;
    var expectRedirectProbe=runtime.expectRedirectProbe;
    var extractEncryptedId=runtime.extractEncryptedId;
    var isFeatureDisabled=runtime.isFeatureDisabled;

    function withAssertions(response, result, assertions, provides) {
        var allAssertions=(result.assertions || []).concat(assertions || []);
        var failed=allAssertions.some(function(assertion){return assertion.outcome==='fail';});
        return {
            outcome: failed ? 'fail' : 'pass', response: response, assertions: allAssertions,
            positiveContractVerified: !failed && allAssertions.every(function(assertion){return assertion.outcome==='pass';}),
            provides: provides || result.provides
        };
    }

    function arrayContract(response, run, options) {
        options=options || {};
        var base=responseResult(response,{status:[200],jsonArray:true,fields:options.fields});
        var assertions=[];
        if(options.paged){assertions=assertions.concat(expectPagedHeaders(run,response,!!options.chartable));}
        return withAssertions(response,base,assertions,options.provides);
    }

    function objectContract(response, run, options) {
        options=options || {};
        var base=responseResult(response,{status:[200],jsonObject:true,fields:options.fields});
        var assertions=[];
        if(options.paged){assertions=assertions.concat(expectPagedHeaders(run,response,!!options.chartable));}
        return withAssertions(response,base,assertions,options.provides);
    }

    function textContract(response, run, contentType, attachment) {
        var base=responseResult(response,{status:[200]});
        var assertions=[expectContentType(response,contentType)];
        if(attachment){assertions.push(expectAttachment(run,response));}
        return withAssertions(response,base,assertions);
    }

    function aeContract(response, run, success) {
        if(isFeatureDisabled(response)){
            return {
                outcome:'pass',response:response,positiveContractVerified:false,
                assertions:[expectStatus(response,[503]),createAssertion('AE feature gate','pass','AE dashboard is disabled; 503 is the verified disabled-mode contract.')]
            };
        }
        return success(response,run);
    }

    function registerArrayRead(config) {
        defineTest({
            id:config.id,phase:2,group:config.group || 'Phase 2 read coverage',evidenceKind:'positive',operationKey:config.operationKey,name:config.name,
            method:'GET',path:config.path,expectedStatus:[200],requires:config.requires,note:config.note,when:config.when,skipReason:config.skipReason,
            request:config.request || {method:'GET',path:config.path,query:config.query},
            evaluate:function(response,context,run){
                if(config.evaluate){return config.evaluate(response,context,run);}
                var evaluate=function(){return arrayContract(response,run,{paged:config.paged,chartable:config.chartable,fields:config.fields,provides:config.provides && config.provides(response,context)});};
                return config.aeFeatureAware ? aeContract(response,run,evaluate) : evaluate();
            }
        });
    }

    function registerObjectRead(config) {
        defineTest({
            id:config.id,phase:2,group:config.group || 'Phase 2 read coverage',evidenceKind:'positive',operationKey:config.operationKey,name:config.name,
            method:'GET',path:config.path,expectedStatus:[200],requires:config.requires,note:config.note,when:config.when,skipReason:config.skipReason,
            request:config.request || {method:'GET',path:config.path,query:config.query},
            evaluate:function(response,context,run){
                if(config.evaluate){return config.evaluate(response,context,run);}
                var evaluate=function(){return objectContract(response,run,{paged:config.paged,chartable:config.chartable,fields:config.fields,provides:config.provides && config.provides(response,context)});};
                return config.aeFeatureAware ? aeContract(response,run,evaluate) : evaluate();
            }
        });
    }

    function registerNegative(config) {
        defineTest({
            id:config.id,phase:3,group:config.group || 'Phase 3 contract and gate coverage',evidenceKind:'negative',operationKey:config.operationKey,name:config.name,
            method:config.method || 'GET',path:config.path,expectedStatus:config.status || [400],requires:config.requires,note:config.note,when:config.when,skipReason:config.skipReason,
            request:config.request || {method:config.method || 'GET',path:config.path,query:config.query,body:config.body},
            evaluate:function(response){return responseResult(response,{status:config.status || [400]});}
        });
    }

    function registerAnonymousGate(config) {
        defineTest({
            id:config.id,phase:3,group:config.group || 'Phase 3 contract and gate coverage',evidenceKind:'authGate',operationKey:config.operationKey,name:config.name,
            method:config.method || 'GET',path:config.path,expectedStatus:[401,403],requires:config.requires,note:config.note,
            when:function(run){return !run.context.authenticated;},skipReason:'Authenticated safety: protected write and gate probes are not issued from an authenticated browser profile.',
            request:config.request || {method:config.method || 'GET',path:config.path,query:config.query,body:config.body},
            evaluate:function(response){return responseResult(response,{status:[401,403]});}
        });
    }

    function register404(config) {
        registerNegative({
            id:config.id,operationKey:config.operationKey,name:config.name,method:config.method || 'GET',path:config.path,request:config.request,requires:config.requires,note:config.note,status:[404],
            when:function(run){return run.options.tarpitMode==='disabled';},skipReason:'Deliberate 404 probes are skipped unless tarpit mode is operator-confirmed disabled.'
        });
    }

    /**************************************************************/
    /**
     * Phase 2 positive-read definitions.
     *
     * @remarks
     * All public read requests are bounded by seeded values and small page sizes. AE requests
     * explicitly preserve the feature-disabled 503 contract rather than treating it as a failure.
     */
    /**************************************************************/
    registerArrayRead({
        id:'read.ae.products',operationKey:'GET /api/AdverseEvent/products',name:'Search AE products with paging evidence',path:'/api/adverseevent/products',
        query:{productSearch:'aspirin',pageNumber:1,pageSize:5},paged:true,aeFeatureAware:true
    });
    registerArrayRead({
        id:'read.ae.catalog',operationKey:'GET /api/AdverseEvent/products/catalog',name:'Read AE product catalog with paging evidence',path:'/api/adverseevent/products/catalog',
        query:{pageNumber:1,pageSize:2},paged:true,aeFeatureAware:true
    });
    defineTest({
        id:'read.ae.count',phase:2,group:'Phase 2 read coverage',evidenceKind:'positive',operationKey:'GET /api/AdverseEvent/products/count',name:'Read AE product count',method:'GET',path:'/api/adverseevent/products/count',expectedStatus:[200],
        request:{method:'GET',path:'/api/adverseevent/products/count'},evaluate:function(response,context,run){return aeContract(response,run,function(){return withAssertions(response,responseResult(response,{status:[200]}),[expectJsonNumber(response)]);});}
    });
    registerObjectRead({id:'read.ae.triage',operationKey:'GET /api/AdverseEvent/products/{documentGuid}/triage',name:'Read AE triage view',path:'/api/adverseevent/products/{documentGuid}/triage',requires:['aeDocumentGuidA'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/products/'+encodeURIComponent(context.aeDocumentGuidA)+'/triage',query:{comparator:'Placebo'}};}});
    registerObjectRead({id:'read.ae.forest',operationKey:'GET /api/AdverseEvent/products/{documentGuid}/forest',name:'Read AE forest view',path:'/api/adverseevent/products/{documentGuid}/forest',requires:['aeDocumentGuidA'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/products/'+encodeURIComponent(context.aeDocumentGuidA)+'/forest'};}});
    registerObjectRead({id:'read.ae.quadrant',operationKey:'GET /api/AdverseEvent/products/{documentGuid}/quadrant',name:'Read AE quadrant view',path:'/api/adverseevent/products/{documentGuid}/quadrant',requires:['aeDocumentGuidA'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/products/'+encodeURIComponent(context.aeDocumentGuidA)+'/quadrant'};}});
    registerObjectRead({id:'read.ae.reverseLookup',operationKey:'GET /api/AdverseEvent/reverse-lookup',name:'Read AE reverse lookup',path:'/api/adverseevent/reverse-lookup',query:{symptom:'nausea'},aeFeatureAware:true});
    registerObjectRead({id:'read.ae.interchange',operationKey:'GET /api/AdverseEvent/interchange',name:'Read AE interchange comparison',path:'/api/adverseevent/interchange',requires:['aeDocumentGuidA','aeDocumentGuidB'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/interchange',query:{documentGuidA:context.aeDocumentGuidA,documentGuidB:context.aeDocumentGuidB}};}});
    registerObjectRead({id:'read.ae.correlation',operationKey:'GET /api/AdverseEvent/correlation',name:'Read pharmacologic-class correlation map',path:'/api/adverseevent/correlation',requires:['pharmClassCode'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/correlation',query:{pharmClassCode:context.pharmClassCode}};}});
    registerArrayRead({id:'read.ae.correlationClasses',operationKey:'GET /api/AdverseEvent/correlation/classes',name:'Read AE pharmacologic-class picker',path:'/api/adverseevent/correlation/classes',query:{pageNumber:1,pageSize:5},paged:true,chartable:true,aeFeatureAware:true});
    registerArrayRead({id:'read.ae.correlationSystems',operationKey:'GET /api/AdverseEvent/correlation/systems',name:'Read AE MedDRA-system picker',path:'/api/adverseevent/correlation/systems',query:{pageNumber:1,pageSize:5},paged:true,chartable:true,aeFeatureAware:true});
    registerObjectRead({id:'read.ae.systemMap',operationKey:'GET /api/AdverseEvent/correlation/systems/map',name:'Read single-system class correlation map',path:'/api/adverseevent/correlation/systems/map',requires:['systemName'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/correlation/systems/map',query:{systems:context.systemName,classPageNumber:1,classPageSize:20}};},provides:function(response){var classes=findValue(response.body,['Classes'],2) || []; var first=classes[0] || {}; var second=classes[1] || first; return {systemClassX:findValue(first,['PharmClassCode'],1),systemClassY:findValue(second,['PharmClassCode'],1)};}});
    registerObjectRead({id:'read.ae.systemHeatmap',operationKey:'GET /api/AdverseEvent/correlation/systems/heatmap',name:'Read single-system class heatmap',path:'/api/adverseevent/correlation/systems/heatmap',requires:['systemName'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/correlation/systems/heatmap',query:{systems:context.systemName,classPageNumber:1,classPageSize:40,drugPageNumber:1,drugPageSize:50}};}});
    registerObjectRead({id:'read.ae.correlationHeatmap',operationKey:'GET /api/AdverseEvent/correlation/heatmap',name:'Read pharmacologic-class heatmap',path:'/api/adverseevent/correlation/heatmap',requires:['pharmClassCode'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/correlation/heatmap',query:{pharmClassCode:context.pharmClassCode}};},provides:function(response){var soc=findValue(response.body,['Soc'],2) || []; return {socX:soc[0],socY:soc[1] || soc[0]};}});    registerObjectRead({id:'read.ae.systemCell',operationKey:'GET /api/AdverseEvent/correlation/systems/cell',name:'Read selected-system class-pair detail',path:'/api/adverseevent/correlation/systems/cell',requires:['systemName','systemClassX','systemClassY'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/correlation/systems/cell',query:{systems:context.systemName,classX:context.systemClassX,classY:context.systemClassY,pageNumber:1,pageSize:100}};}});
    registerObjectRead({id:'read.ae.correlationCell',operationKey:'GET /api/AdverseEvent/correlation/cell',name:'Read pharmacologic-class SOC-pair detail',path:'/api/adverseevent/correlation/cell',requires:['pharmClassCode','socX','socY'],aeFeatureAware:true,request:function(context){return {method:'GET',path:'/api/adverseevent/correlation/cell',query:{pharmClassCode:context.pharmClassCode,socX:context.socX,socY:context.socY}};}});

    registerObjectRead({id:'read.orangeBook.expiring',operationKey:'GET /api/OrangeBook/expiring',name:'Read expiring Orange Book patents',path:'/api/orangebook/expiring',query:{tradeName:'Ozempic',pageNumber:1,pageSize:5},paged:true,fields:['Patents','Markdown','TotalCount','TotalPages']});

    registerArrayRead({id:'read.label.productSearch',operationKey:'GET /api/Label/product/search',name:'Search label products',path:'/api/label/product/search',requires:['productName'],paged:true,request:function(context){return {method:'GET',path:'/api/label/product/search',query:{productNameSearch:context.productName,pageNumber:1,pageSize:5}};}});
    registerArrayRead({id:'read.label.productRelated',operationKey:'GET /api/Label/product/related',name:'Read related label products',path:'/api/label/product/related',requires:['labelDocumentGuid'],request:function(context){return {method:'GET',path:'/api/label/product/related',query:{sourceDocumentGuid:context.labelDocumentGuid,pageNumber:1,pageSize:5}};}});
    registerArrayRead({id:'read.label.productLatestDetails',operationKey:'GET /api/Label/product/latest/details',name:'Read latest label details',path:'/api/label/product/latest/details',requires:['unii'],paged:true,fields:['ViewLabelUrl'],request:function(context){return {method:'GET',path:'/api/label/product/latest/details',query:{unii:context.unii,pageNumber:1,pageSize:2}};}});
    registerArrayRead({id:'read.label.productIndications',operationKey:'GET /api/Label/product/indications',name:'Read label product indications',path:'/api/label/product/indications',requires:['unii'],paged:true,request:function(context){return {method:'GET',path:'/api/label/product/indications',query:{unii:context.unii,pageNumber:1,pageSize:2}};}});
    registerArrayRead({id:'read.label.ingredientSearch',operationKey:'GET /api/Label/ingredient/search',name:'Search label ingredients',path:'/api/label/ingredient/search',requires:['unii'],paged:true,request:function(context){return {method:'GET',path:'/api/label/ingredient/search',query:{unii:context.unii,pageNumber:1,pageSize:2}};}});
    registerArrayRead({id:'read.label.ingredientSummaries',operationKey:'GET /api/Label/ingredient/summaries',name:'Read ingredient summaries',path:'/api/label/ingredient/summaries',query:{pageNumber:1,pageSize:2},paged:true});
    registerArrayRead({id:'read.label.activeIngredientSummaries',operationKey:'GET /api/Label/ingredient/active/summaries',name:'Read active-ingredient summaries',path:'/api/label/ingredient/active/summaries',query:{pageNumber:1,pageSize:2},paged:true});
    registerArrayRead({id:'read.label.inactiveIngredientSummaries',operationKey:'GET /api/Label/ingredient/inactive/summaries',name:'Read inactive-ingredient summaries',path:'/api/label/ingredient/inactive/summaries',query:{pageNumber:1,pageSize:2},paged:true});
    registerArrayRead({id:'read.label.ingredientAdvanced',operationKey:'GET /api/Label/ingredient/advanced',name:'Read advanced ingredient results',path:'/api/label/ingredient/advanced',requires:['unii'],paged:true,request:function(context){return {method:'GET',path:'/api/label/ingredient/advanced',query:{unii:context.unii,pageNumber:1,pageSize:2}};}});
    registerArrayRead({id:'read.label.ingredientByApplication',operationKey:'GET /api/Label/ingredient/by-application',name:'Read ingredients by application',path:'/api/label/ingredient/by-application',requires:['applicationNumber'],paged:true,request:function(context){return {method:'GET',path:'/api/label/ingredient/by-application',query:{applicationNumber:context.applicationNumber,pageNumber:1,pageSize:2}};}});
    registerObjectRead({id:'read.label.ingredientRelated',operationKey:'GET /api/Label/ingredient/related',name:'Read related ingredients',path:'/api/label/ingredient/related',requires:['substanceName'],request:function(context){return {method:'GET',path:'/api/label/ingredient/related',query:{substanceNameSearch:context.substanceName}};}});
    registerArrayRead({id:'read.label.ndcSearch',operationKey:'GET /api/Label/ndc/search',name:'Read products by NDC',path:'/api/label/ndc/search',requires:['productCode'],paged:true,request:function(context){return {method:'GET',path:'/api/label/ndc/search',query:{productCode:context.productCode,pageNumber:1,pageSize:2}};}});
    registerArrayRead({id:'read.label.ndcPackageSearch',operationKey:'GET /api/Label/ndc/package/search',name:'Read packages by NDC',path:'/api/label/ndc/package/search',requires:['productCode'],paged:true,request:function(context){return {method:'GET',path:'/api/label/ndc/package/search',query:{packageCode:context.productCode,pageNumber:1,pageSize:2}};}});
    registerArrayRead({id:'read.label.labelerSearch',operationKey:'GET /api/Label/labeler/search',name:'Search labelers',path:'/api/label/labeler/search',requires:['labelerName'],paged:true,request:function(context){return {method:'GET',path:'/api/label/labeler/search',query:{labelerNameSearch:context.labelerName,pageNumber:1,pageSize:2}};}});
    registerArrayRead({id:'read.label.pharmClassSearch',operationKey:'GET /api/Label/pharmacologic-class/search',name:'Search pharmacologic classes without AI',path:'/api/label/pharmacologic-class/search',query:{classNameSearch:'Beta',pageNumber:1,pageSize:2},paged:true});
    registerArrayRead({id:'read.label.pharmClassHierarchy',operationKey:'GET /api/Label/pharmacologic-class/hierarchy',name:'Read pharmacologic-class hierarchy',path:'/api/label/pharmacologic-class/hierarchy',query:{useAiCache:false},paged:false});
    registerArrayRead({id:'read.label.pharmClassSummaries',operationKey:'GET /api/Label/pharmacologic-class/summaries',name:'Read pharmacologic-class summaries',path:'/api/label/pharmacologic-class/summaries',query:{useAiCache:false,pageNumber:1,pageSize:2},paged:true});
    registerArrayRead({id:'read.label.deaSchedule',operationKey:'GET /api/Label/drug-safety/dea-schedule',name:'Read DEA schedule labels',path:'/api/label/drug-safety/dea-schedule',query:{scheduleCode:'CII',pageNumber:1,pageSize:2},paged:true});
    registerArrayRead({id:'read.label.guide',operationKey:'GET /api/Label/guide',name:'Read label guide metadata',path:'/api/label/guide',query:{category:'Search'},paged:false});
    registerArrayRead({id:'read.label.inventorySummary',operationKey:'GET /api/Label/inventory/summary',name:'Read label inventory summary',path:'/api/label/inventory/summary',query:{category:'TOTALS'},paged:false});
    registerArrayRead({id:'read.label.applicationSearch',operationKey:'GET /api/Label/application-number/search',name:'Search label applications',path:'/api/label/application-number/search',requires:['applicationNumber'],paged:true,request:function(context){return {method:'GET',path:'/api/label/application-number/search',query:{applicationNumber:context.applicationNumber,pageNumber:1,pageSize:2}};}});
    registerArrayRead({id:'read.label.sectionSearch',operationKey:'GET /api/Label/section/search',name:'Search label sections',path:'/api/label/section/search',requires:['sectionCode'],paged:true,request:function(context){return {method:'GET',path:'/api/label/section/search',query:{sectionCode:context.sectionCode,pageNumber:1,pageSize:2}};}});
    registerArrayRead({id:'read.label.sectionContent',operationKey:'GET /api/Label/section/content/{documentGuid}',name:'Read label section content',path:'/api/label/section/content/{documentGuid}',requires:['labelDocumentGuid','sectionCode'],paged:true,request:function(context){return {method:'GET',path:'/api/label/section/content/'+encodeURIComponent(context.labelDocumentGuid),query:{sectionCode:context.sectionCode,pageNumber:1,pageSize:2}};}});
    registerObjectRead({id:'read.label.sectionDocumentation',operationKey:'GET /api/Label/{menuSelection}/documentation',name:'Read label section documentation',path:'/api/label/{menuSelection}/documentation',requires:['menuSelection'],request:function(context){return {method:'GET',path:'/api/label/'+encodeURIComponent(context.menuSelection)+'/documentation'};}});
    registerObjectRead({id:'read.label.sectionRecord',operationKey:'GET /api/Label/{menuSelection}/{encryptedId}',name:'Read seeded label section record',path:'/api/label/{menuSelection}/{encryptedId}',requires:['menuSelection','encryptedId'],request:function(context){return {method:'GET',path:'/api/label/'+encodeURIComponent(context.menuSelection)+'/'+encodeURIComponent(context.encryptedId)};}});
    registerArrayRead({id:'read.label.versionHistory',operationKey:'GET /api/Label/document/version-history/{setGuidOrDocumentGuid}',name:'Read label document version history',path:'/api/label/document/version-history/{setGuidOrDocumentGuid}',requires:['setGuid'],request:function(context){return {method:'GET',path:'/api/label/document/version-history/'+encodeURIComponent(context.setGuid)};}});
    registerObjectRead({id:'read.label.single',operationKey:'GET /api/Label/single/{documentGuid}',name:'Read one label document and document header',path:'/api/label/single/{documentGuid}',requires:['labelDocumentGuid'],request:function(context){return {method:'GET',path:'/api/label/single/'+encodeURIComponent(context.labelDocumentGuid)};},evaluate:function(response,context,run){return withAssertions(response,objectContract(response,run),[expectObservableHeader(run,response,'X-Document-Guid')]);}});
    registerArrayRead({id:'read.label.complete',operationKey:'GET /api/Label/complete/{pageNumber?}/{pageSize?}',name:'Read bounded complete label graph',path:'/api/label/complete/{pageNumber?}/{pageSize?}',paged:true,request:{method:'GET',path:'/api/label/complete/1/1'}});
    defineTest({id:'read.label.generateXml',phase:2,group:'Phase 2 read coverage',evidenceKind:'positive',operationKey:'GET /api/Label/generate/{documentGuid}/{minify}',name:'Generate label XML',method:'GET',path:'/api/label/generate/{documentGuid}/{minify}',expectedStatus:[200],requires:['labelDocumentGuid'],request:function(context){return {method:'GET',path:'/api/label/generate/'+encodeURIComponent(context.labelDocumentGuid)+'/true',headers:{Accept:'application/xml'}};},evaluate:function(response,context,run){return withAssertions(response,textContract(response,run,'application/xml'),[expectXmlDocument(response)]);}});
    defineTest({id:'read.label.originalXml',phase:2,group:'Phase 2 read coverage',evidenceKind:'positive',operationKey:'GET /api/Label/original/{documentGuid}/{minify}',name:'Read original label XML',method:'GET',path:'/api/label/original/{documentGuid}/{minify}',expectedStatus:[200],requires:['labelDocumentGuid'],note:'A disabled XML-export feature returns 503 and is reported as non-positive evidence.',request:function(context){return {method:'GET',path:'/api/label/original/'+encodeURIComponent(context.labelDocumentGuid)+'/false',headers:{Accept:'application/xml'}};},evaluate:function(response,context,run){if(response.status===503){return {outcome:'pass',response:response,positiveContractVerified:false,assertions:[expectStatus(response,[503]),createAssertion('XML export feature gate','pass','Original XML export is disabled.')]};}return withAssertions(response,textContract(response,run,'application/xml'),[expectXmlDocument(response)]);}});
    registerArrayRead({id:'read.label.markdownSections',operationKey:'GET /api/Label/markdown/sections/{documentGuid}',name:'Read generated markdown sections',path:'/api/label/markdown/sections/{documentGuid}',requires:['labelDocumentGuid'],note:'A document without generated markdown returns 404 and is reported as non-positive evidence.',request:function(context){return {method:'GET',path:'/api/label/markdown/sections/'+encodeURIComponent(context.labelDocumentGuid)};},evaluate:function(response,context,run){if(response.status===404){return {outcome:'pass',response:response,positiveContractVerified:false,assertions:[expectStatus(response,[404]),createAssertion('Markdown availability','pass','The selected document has no generated markdown.')]};}return arrayContract(response,run);}});
    registerObjectRead({id:'read.label.markdownExport',operationKey:'GET /api/Label/markdown/export/{documentGuid}',name:'Read markdown export object',path:'/api/label/markdown/export/{documentGuid}',requires:['labelDocumentGuid'],request:function(context){return {method:'GET',path:'/api/label/markdown/export/'+encodeURIComponent(context.labelDocumentGuid)};}});
    defineTest({id:'read.label.markdownDownload',phase:2,group:'Phase 2 read coverage',evidenceKind:'positive',operationKey:'GET /api/Label/markdown/download/{documentGuid}',name:'Download label markdown attachment',method:'GET',path:'/api/label/markdown/download/{documentGuid}',expectedStatus:[200],requires:['labelDocumentGuid'],request:function(context){return {method:'GET',path:'/api/label/markdown/download/'+encodeURIComponent(context.labelDocumentGuid),headers:{Accept:'text/markdown'}};},evaluate:function(response,context,run){return textContract(response,run,'text/markdown',true);}});

    registerObjectRead({id:'read.settings.demoMode',operationKey:'GET /api/Settings/demomode',name:'Read demo mode settings',path:'/api/settings/demomode'});
    registerObjectRead({id:'read.settings.databaseLimits',operationKey:'GET /api/Settings/database-limits',name:'Read database limits settings',path:'/api/settings/database-limits'});
    registerObjectRead({id:'read.ai.conversation',operationKey:'GET /api/Ai/conversations/{conversationId}',name:'Read loopback test conversation',path:'/api/ai/conversations/{conversationId}',requires:['conversationId'],request:function(context){return {method:'GET',path:'/api/ai/conversations/'+encodeURIComponent(context.conversationId)};}});
    registerArrayRead({id:'read.ai.conversationHistory',operationKey:'GET /api/Ai/conversations/{conversationId}/history',name:'Read loopback test conversation history',path:'/api/ai/conversations/{conversationId}/history',requires:['conversationId'],request:function(context){return {method:'GET',path:'/api/ai/conversations/'+encodeURIComponent(context.conversationId)+'/history'};}});
    registerObjectRead({id:'read.ai.conversationStats',operationKey:'GET /api/Ai/conversations/stats',name:'Read conversation statistics literal route',path:'/api/ai/conversations/stats'});
    defineTest({id:'read.ai.deleteConversation',phase:2,group:'Phase 2 read coverage',evidenceKind:'positive',operationKey:'DELETE /api/Ai/conversations/{conversationId}',name:'Delete loopback test conversation',method:'DELETE',path:'/api/ai/conversations/{conversationId}',expectedStatus:[200],requires:['conversationId'],request:function(context){return {method:'DELETE',path:'/api/ai/conversations/'+encodeURIComponent(context.conversationId)};},evaluate:function(response){return responseResult(response,{status:[200]});}});
    defineTest({id:'phase2.seedCompleteness',phase:2,group:'Phase 2 read coverage',evidenceKind:'positive',name:'Require complete seeded data evidence before accepting read coverage',internal:function(run){var incomplete=run.report.tests.filter(function(test){return test.id.indexOf('seed.')===0 && test.outcome==='skip' && !(test.id.indexOf('seed.ae.')===0 && /Feature is disabled/.test(test.skipReason || ''));}); var passed=!incomplete.length; return {outcome:passed?'pass':'fail',positiveContractVerified:passed,assertions:[createAssertion('Seed completeness',passed?'pass':'fail',passed?'All required seed evidence is available.':'Unexpected skipped seed definitions: '+incomplete.map(function(test){return test.id;}).join(', ')+'.')]};}});
    defineTest({id:'read.auth.externalLogin',phase:2,group:'Phase 2 read coverage',evidenceKind:'positive',operationKey:'GET /api/Auth/external-login',name:'Read external-login information',method:'GET',path:'/api/auth/external-login',expectedStatus:[200],request:{method:'GET',path:'/api/auth/external-login',headers:{Accept:'text/plain'}},evaluate:function(response){return responseResult(response,{status:[200]});}});
    /**************************************************************/
    /**
     * Phase 3 contract, negative, and authorization-gate definitions.
     *
     * @remarks
     * No authenticated profile sends a protected write probe. Deliberate 404 probes are capped
     * and run only when the operator has attested that tarpit monitoring is disabled.
     */
    /**************************************************************/
    registerNegative({id:'negative.ae.productsPaging',operationKey:'GET /api/AdverseEvent/products',name:'Reject AE product page zero',path:'/api/adverseevent/products',query:{productSearch:'aspirin',pageNumber:0,pageSize:5}});
    registerAnonymousGate({id:'gate.ae.favorites',operationKey:'GET /api/AdverseEvent/products/favorites',name:'Gate anonymous AE favorites read',path:'/api/adverseevent/products/favorites',query:{pageNumber:1,pageSize:1}});
    registerAnonymousGate({id:'gate.ae.favoritePut',operationKey:'PUT /api/AdverseEvent/products/{documentGuid}/favorite',name:'Gate anonymous AE favorite write',method:'PUT',path:'/api/adverseevent/products/00000000-0000-0000-0000-000000000000/favorite'});
    registerAnonymousGate({id:'gate.ae.favoriteDelete',operationKey:'DELETE /api/AdverseEvent/products/{documentGuid}/favorite',name:'Gate anonymous AE unfavorite write',method:'DELETE',path:'/api/adverseevent/products/00000000-0000-0000-0000-000000000000/favorite'});
    registerNegative({id:'negative.ae.reverseLookup',operationKey:'GET /api/AdverseEvent/reverse-lookup',name:'Reject AE reverse lookup without symptom',path:'/api/adverseevent/reverse-lookup'});
    registerNegative({id:'negative.ae.interchangeSameProduct',operationKey:'GET /api/AdverseEvent/interchange',name:'Reject AE interchange of the same product',path:'/api/adverseevent/interchange',requires:['aeDocumentGuidA'],request:function(context){return {method:'GET',path:'/api/adverseevent/interchange',query:{documentGuidA:context.aeDocumentGuidA,documentGuidB:context.aeDocumentGuidA}};}});
    registerNegative({id:'negative.ae.correlationBlank',operationKey:'GET /api/AdverseEvent/correlation',name:'Reject AE correlation without class code',path:'/api/adverseevent/correlation'});
    registerNegative({id:'negative.ae.systemMapMultiple',operationKey:'GET /api/AdverseEvent/correlation/systems/map',name:'Reject multiple systems for a single-system map',path:'/api/adverseevent/correlation/systems/map',requires:['systemName'],request:function(context){return {method:'GET',path:'/api/adverseevent/correlation/systems/map',query:{systems:[context.systemName,context.systemName],classPageNumber:1,classPageSize:20}};}});
    registerNegative({id:'negative.ae.systemCellMissingClass',operationKey:'GET /api/AdverseEvent/correlation/systems/cell',name:'Reject system correlation cell without class X',path:'/api/adverseevent/correlation/systems/cell',requires:['systemName'],request:function(context){return {method:'GET',path:'/api/adverseevent/correlation/systems/cell',query:{systems:context.systemName,classY:'Example',pageNumber:1,pageSize:100}};}});
    registerNegative({id:'negative.ae.correlationCellMissingSoc',operationKey:'GET /api/AdverseEvent/correlation/cell',name:'Reject correlation cell without SOC Y',path:'/api/adverseevent/correlation/cell',requires:['pharmClassCode'],request:function(context){return {method:'GET',path:'/api/adverseevent/correlation/cell',query:{pharmClassCode:context.pharmClassCode,socX:'Cardiac Disorders'}};}});

    registerNegative({id:'negative.orangeBook.missingSearch',operationKey:'GET /api/OrangeBook/expiring',name:'Reject Orange Book query without a search filter',path:'/api/orangebook/expiring'});

    registerNegative({id:'negative.label.productSearch',operationKey:'GET /api/Label/product/search',name:'Reject empty product search',path:'/api/label/product/search',query:{productNameSearch:''}});
    registerNegative({id:'negative.label.productRelated',operationKey:'GET /api/Label/product/related',name:'Reject related-product query without a source',path:'/api/label/product/related'});
    registerNegative({id:'negative.label.ingredientSearch',operationKey:'GET /api/Label/ingredient/search',name:'Reject ingredient search without a filter',path:'/api/label/ingredient/search'});
    registerNegative({id:'negative.label.ingredientSummariesMinCount',operationKey:'GET /api/Label/ingredient/summaries',name:'Reject negative ingredient minimum product count',path:'/api/label/ingredient/summaries',query:{minProductCount:-1}});
    registerNegative({id:'negative.label.ingredientAdvanced',operationKey:'GET /api/Label/ingredient/advanced',name:'Reject advanced ingredient query without a filter',path:'/api/label/ingredient/advanced'});
    registerNegative({id:'negative.label.ingredientByApplication',operationKey:'GET /api/Label/ingredient/by-application',name:'Reject ingredient-by-application without an application',path:'/api/label/ingredient/by-application'});
    registerNegative({id:'negative.label.ndcSearch',operationKey:'GET /api/Label/ndc/search',name:'Reject empty NDC product search',path:'/api/label/ndc/search',query:{productCode:''}});
    registerNegative({id:'negative.label.ndcPackageSearch',operationKey:'GET /api/Label/ndc/package/search',name:'Reject empty NDC package search',path:'/api/label/ndc/package/search',query:{packageCode:''}});
    registerNegative({id:'negative.label.labelerSearch',operationKey:'GET /api/Label/labeler/search',name:'Reject empty labeler search',path:'/api/label/labeler/search',query:{labelerNameSearch:''}});
    registerNegative({id:'negative.label.pharmClassSearch',operationKey:'GET /api/Label/pharmacologic-class/search',name:'Reject pharmacologic-class search without a filter',path:'/api/label/pharmacologic-class/search'});
    registerNegative({id:'negative.label.extractProduct',operationKey:'GET /api/Label/extract-product',name:'Reject product extraction without description',path:'/api/label/extract-product'});
    registerNegative({id:'negative.label.indicationSearch',operationKey:'GET /api/Label/indication/search',name:'Reject indication search without query',path:'/api/label/indication/search'});
    registerNegative({id:'negative.label.applicationSearch',operationKey:'GET /api/Label/application-number/search',name:'Reject empty application search',path:'/api/label/application-number/search',query:{applicationNumber:''}});
    registerNegative({id:'negative.label.sectionSearch',operationKey:'GET /api/Label/section/search',name:'Reject empty label section search',path:'/api/label/section/search',query:{sectionCode:''}});
    registerNegative({id:'negative.label.navigationPaging',operationKey:'GET /api/Label/document/navigation',name:'Reject label document page zero',path:'/api/label/document/navigation',query:{pageNumber:0,pageSize:1}});
    register404({id:'negative.label.versionHistoryNotFound',operationKey:'GET /api/Label/document/version-history/{setGuidOrDocumentGuid}',name:'Return not found for a nonexistent label version history',path:'/api/label/document/version-history/{setGuidOrDocumentGuid}',request:{method:'GET',path:'/api/label/document/version-history/00000000-0000-0000-0000-000000000000'}});
    register404({id:'negative.label.generateRouteConstraint',operationKey:'GET /api/Label/generate/{documentGuid}/{minify}',name:'Prove generated-label route GUID constraint',path:'/api/label/generate/{documentGuid}/{minify}',request:{method:'GET',path:'/api/label/generate/not-a-guid/false'}});
    register404({id:'negative.label.markdownDisplayMissing',operationKey:'GET /api/Label/markdown/display/{documentGuid}',name:'Return not found for absent cached markdown',path:'/api/label/markdown/display/{documentGuid}',request:{method:'GET',path:'/api/label/markdown/display/00000000-0000-0000-0000-000000000000'}});
    registerNegative({id:'negative.label.comparisonEmptyGuid',operationKey:'GET /api/Label/comparison/analysis/{documentGuid}',name:'Reject comparison analysis for empty GUID',path:'/api/label/comparison/analysis/{documentGuid}',request:{method:'GET',path:'/api/label/comparison/analysis/00000000-0000-0000-0000-000000000000'}});
    register404({id:'negative.label.comparisonProgressMissing',operationKey:'GET /api/Label/comparison/progress/{operationId}',name:'Return not found for absent comparison progress',path:'/api/label/comparison/progress/{operationId}',request:{method:'GET',path:'/api/label/comparison/progress/missing-operation'}});
    registerAnonymousGate({id:'gate.label.createSection',operationKey:'POST /api/Label/{menuSelection}',name:'Gate anonymous label section creation',method:'POST',path:'/api/label/Document',body:{}});
    registerAnonymousGate({id:'gate.label.updateSection',operationKey:'PUT /api/Label/{menuSelection}/{encryptedId}',name:'Gate anonymous label section update',method:'PUT',path:'/api/label/Document/not-a-real-encrypted-id',body:{}});
    registerAnonymousGate({id:'gate.label.deleteSection',operationKey:'DELETE /api/Label/{menuSelection}/{encryptedId}',name:'Gate anonymous label section delete',method:'DELETE',path:'/api/label/Document/not-a-real-encrypted-id'});
    registerAnonymousGate({id:'gate.label.startComparison',operationKey:'POST /api/Label/comparison/analysis/{documentGuid}',name:'Gate anonymous label comparison start',method:'POST',path:'/api/label/comparison/analysis/00000000-0000-0000-0000-000000000000'});
    registerAnonymousGate({id:'gate.label.import',operationKey:'POST /api/Label/import',name:'Gate anonymous label import',method:'POST',path:'/api/label/import'});
    register404({id:'negative.label.importProgressMissing',operationKey:'GET /api/Label/import/progress/{operationId}',name:'Return not found for absent import progress',path:'/api/label/import/progress/{operationId}',request:{method:'GET',path:'/api/label/import/progress/missing-operation'}});

    defineTest({id:'safety.settings.clearManagedCache',phase:3,group:'Phase 3 contract and gate coverage',evidenceKind:'safetyExcluded',operationKey:'POST /api/Settings/clearmanagedcache',name:'Exclude global managed-cache clearing from Profile A',method:'POST',path:'/api/settings/clearmanagedcache',expectedStatus:[200],internal:function(){return {outcome:'skip',skipReason:'Safety exclusion: shared managed-cache clearing is not invoked without explicit local mutation confirmation.',assertions:[createAssertion('Safety exclusion','skip','No request was issued because the operation has a non-reverting shared-state effect.')]};}});
    registerAnonymousGate({id:'gate.settings.databaseCost',operationKey:'GET /api/Settings/metrics/database-cost',name:'Gate anonymous database-cost metrics',path:'/api/settings/metrics/database-cost'});
    registerAnonymousGate({id:'gate.settings.appCredential',operationKey:'GET /api/Settings/test/app-credential',name:'Gate anonymous app-credential test',path:'/api/settings/test/app-credential'});
    registerAnonymousGate({id:'gate.settings.appMetrics',operationKey:'GET /api/Settings/test/app-metrics-pipeline',name:'Gate anonymous app-metrics-pipeline test',path:'/api/settings/test/app-metrics-pipeline'});
    registerAnonymousGate({id:'gate.settings.logs',operationKey:'GET /api/Settings/logs',name:'Gate anonymous logs read',path:'/api/settings/logs',query:{pageNumber:1,pageSize:10}});
    registerAnonymousGate({id:'gate.settings.logStatistics',operationKey:'GET /api/Settings/logs/statistics',name:'Gate anonymous log statistics',path:'/api/settings/logs/statistics'});
    registerAnonymousGate({id:'gate.settings.logCategories',operationKey:'GET /api/Settings/logs/categories',name:'Gate anonymous log categories',path:'/api/settings/logs/categories'});
    registerAnonymousGate({id:'gate.settings.logUsers',operationKey:'GET /api/Settings/logs/users',name:'Gate anonymous log users',path:'/api/settings/logs/users'});
    registerAnonymousGate({id:'gate.settings.logsByDate',operationKey:'GET /api/Settings/logs/by-date',name:'Gate anonymous logs by date',path:'/api/settings/logs/by-date',query:{startDate:'2026-07-16',endDate:'2026-07-17',pageNumber:1,pageSize:10}});
    registerAnonymousGate({id:'gate.settings.logsByCategory',operationKey:'GET /api/Settings/logs/by-category',name:'Gate anonymous logs by category',path:'/api/settings/logs/by-category',query:{category:'Info',pageNumber:1,pageSize:10}});
    registerAnonymousGate({id:'gate.settings.logsByUser',operationKey:'GET /api/Settings/logs/by-user',name:'Gate anonymous logs by user',path:'/api/settings/logs/by-user',query:{pageNumber:1,pageSize:10}});

    registerNegative({id:'negative.ai.interpret',operationKey:'POST /api/Ai/interpret',name:'Reject AI interpretation without a message',method:'POST',path:'/api/ai/interpret',body:{userMessage:''}});
    registerNegative({id:'negative.ai.synthesize',operationKey:'POST /api/Ai/synthesize',name:'Reject AI synthesis without an executed endpoint payload',method:'POST',path:'/api/ai/synthesize',body:{}});
    registerNegative({id:'negative.ai.chat',operationKey:'GET /api/Ai/chat',name:'Reject AI chat without a message',path:'/api/ai/chat'});
    registerNegative({id:'negative.ai.retry',operationKey:'POST /api/Ai/retry',name:'Reject AI retry without failed results',method:'POST',path:'/api/ai/retry',body:{}});
    register404({id:'negative.ai.deletedConversation',operationKey:'GET /api/Ai/conversations/{conversationId}',name:'Verify deleted loopback conversation is absent',path:'/api/ai/conversations/{conversationId}',requires:['conversationId'],request:function(context){return {method:'GET',path:'/api/ai/conversations/'+encodeURIComponent(context.conversationId)};}});

    registerAnonymousGate({id:'gate.users.me',operationKey:'GET /api/Users/me',name:'Gate anonymous current-user read',path:'/api/users/me'});
    registerAnonymousGate({id:'gate.users.list',operationKey:'GET /api/Users',name:'Gate anonymous user list',path:'/api/users'});
    registerAnonymousGate({id:'gate.users.byEmail',operationKey:'GET /api/Users/byemail',name:'Gate anonymous user email lookup',path:'/api/users/byemail',query:{email:'nobody@example.invalid'}});
    registerAnonymousGate({id:'gate.users.byId',operationKey:'GET /api/Users/{encryptedUserId}',name:'Gate anonymous user lookup by ID',path:'/api/users/not-a-real-encrypted-id'});
    registerAnonymousGate({id:'gate.users.activity',operationKey:'GET /api/Users/user/{encryptedUserId}/activity',name:'Gate anonymous user activity',path:'/api/users/user/not-a-real-encrypted-id/activity'});
    registerAnonymousGate({id:'gate.users.activityDateRange',operationKey:'GET /api/Users/user/{encryptedUserId}/activity/daterange',name:'Gate anonymous user activity date range',path:'/api/users/user/not-a-real-encrypted-id/activity/daterange',query:{startDate:'2026-07-16',endDate:'2026-07-17'}});
    registerAnonymousGate({id:'gate.users.endpointStats',operationKey:'GET /api/Users/endpoint-stats',name:'Gate anonymous user endpoint statistics',path:'/api/users/endpoint-stats'});
    registerNegative({id:'negative.users.signUp',operationKey:'POST /api/Users/signup',name:'Reject invalid signup without creating a user',method:'POST',path:'/api/users/signup',body:{email:'test@example.invalid',password:'NotARealPassword1!',confirmPassword:'mismatch'}});
    registerNegative({id:'negative.users.authenticate',operationKey:'POST /api/Users/authenticate',name:'Reject incomplete authentication model without lockout risk',method:'POST',path:'/api/users/authenticate',body:{email:'nobody@example.invalid'}});
    registerAnonymousGate({id:'gate.users.updateProfile',operationKey:'PUT /api/Users/{encryptedUserId}/profile',name:'Gate anonymous profile update',method:'PUT',path:'/api/users/not-a-real-encrypted-id/profile',body:{}});
    registerAnonymousGate({id:'gate.users.delete',operationKey:'DELETE /api/Users/{encryptedUserId}',name:'Gate anonymous user deletion',method:'DELETE',path:'/api/users/not-a-real-encrypted-id'});
    registerAnonymousGate({id:'gate.users.adminUpdate',operationKey:'PUT /api/Users/admin-update',name:'Gate anonymous admin user update',method:'PUT',path:'/api/users/admin-update',body:{}});
    registerAnonymousGate({id:'gate.users.rotatePassword',operationKey:'POST /api/Users/rotate-password',name:'Gate anonymous password rotation',method:'POST',path:'/api/users/rotate-password',body:{}});
    registerAnonymousGate({id:'gate.users.resolveMcp',operationKey:'POST /api/Users/resolve-mcp',name:'Gate browser cookie from MCP bearer resolution',method:'POST',path:'/api/users/resolve-mcp',body:{}});

    registerNegative({id:'negative.auth.tokenPlaceholder',operationKey:'POST /api/Auth/token-placeholder',name:'Reject token placeholder request',method:'POST',path:'/api/auth/token-placeholder',body:{}});
    defineTest({id:'gate.auth.externalLoginRedirect',phase:3,group:'Phase 3 contract and gate coverage',evidenceKind:'authGate',operationKey:'GET /api/Auth/login/{provider}',name:'Probe external login without following OAuth redirect',method:'GET',path:'/api/auth/login/{provider}',expectedStatus:[0,503],request:{method:'GET',path:'/api/auth/login/google',credentials:'omit',redirect:'manual'},evaluate:function(response){if(response.transportError){return responseResult(response,{status:[0,503]});}var assertion=expectRedirectProbe(response);return {outcome:assertion.outcome==='pass'?'pass':'fail',response:response,assertions:[assertion],positiveContractVerified:false};}});
    defineTest({id:'gate.auth.externalCallbackRedirect',phase:3,group:'Phase 3 contract and gate coverage',evidenceKind:'authGate',operationKey:'GET /api/Auth/external-logincallback',name:'Probe external callback without OAuth state',method:'GET',path:'/api/auth/external-logincallback',expectedStatus:[0],request:{method:'GET',path:'/api/auth/external-logincallback',credentials:'omit',redirect:'manual'},evaluate:function(response){if(response.transportError){return responseResult(response,{status:[0]});}var assertion=expectRedirectProbe(response);return {outcome:assertion.outcome==='pass'?'pass':'fail',response:response,assertions:[assertion],positiveContractVerified:false};}});
    registerNegative({id:'negative.auth.loginFailure',operationKey:'GET /api/Auth/loginfailure',name:'Return 400 login failure explanation',path:'/api/auth/loginfailure'});
    registerAnonymousGate({id:'gate.auth.lockout',operationKey:'GET /api/Auth/lockout',name:'Return lockout gate',path:'/api/auth/lockout'});
    registerAnonymousGate({id:'gate.auth.logout',operationKey:'POST /api/Auth/logout',name:'Gate anonymous logout',method:'POST',path:'/api/auth/logout'});
    registerAnonymousGate({id:'gate.auth.login',operationKey:'GET /api/Auth/login',name:'Return login-required instruction',path:'/api/auth/login'});
    registerAnonymousGate({id:'gate.auth.accessDenied',operationKey:'GET /api/Auth/accessdenied',name:'Return access-denied gate',path:'/api/auth/accessdenied'});
})(window.MedRecProApiTestRuntime);
