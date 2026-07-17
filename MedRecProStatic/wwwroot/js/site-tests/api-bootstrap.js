/**************************************************************/
/**
 * MedRecPro API Test Bootstrap Module
 *
 * @fileoverview Connects the split classic-script API diagnostic to the legacy MedRecProTests browser-console surface after all dependencies have loaded.
 *
 * @module site-tests/api-bootstrap
 */
/**************************************************************/
(function (siteTests, runtime) {
    'use strict';

    if (!siteTests || !runtime) {
        throw new Error('MedRecPro site tests and API test runtime must load before bootstrap.');
    }

    /**************************************************************/
    /**
     * Runs legacy DOM checks followed by the selected API diagnostic.
     *
     * @param {Object} [options] API diagnostic options.
     * @returns {Promise<Object>} Combined DOM and API reports.
     */
    /**************************************************************/
    function runEverything(options) {
        var uiResults = siteTests.runAll();
        return runtime.runApiTests(options).then(function (apiReport) {
            return { ui: uiResults, api: apiReport };
        });
    }

    siteTests.runApiTests = runtime.runApiTests;
    siteTests.runEverything = runEverything;
    runtime.initializeQueryTrigger();
})(window.MedRecProTests, window.MedRecProApiTestRuntime);
