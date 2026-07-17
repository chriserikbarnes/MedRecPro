/**************************************************************/
/**
 * MedRecPro Legacy Site Test Entry Point
 *
 * @fileoverview Provides browser-console DOM smoke checks for navigation, MCP, chat, animation, and layout elements.
 *
 * @description Keeps the established `MedRecProTests.runAll()` API independent from the endpoint diagnostic modules loaded later in the page.
 *
 * @example
 * MedRecProTests.runAll();
 *
 * @module site-tests
 * @see MedRecProApiTestRuntime
 */
/**************************************************************/

var MedRecProTests = (function () {
    'use strict';

    var results = { passed: 0, failed: 0, tests: [] };

    /**************************************************************/

    /**

     * Runs a scoped legacy DOM assertion for the static-site smoke suite.

     *

     * @private

     */

    /**************************************************************/

    function assert(name, condition, message) {
        results.tests.push({
            name: name,
            passed: condition,
            message: condition ? 'OK' : (message || 'Failed')
        });
        if (condition) {
            results.passed++;
        } else {
            results.failed++;
        }
    }

    /**************************************************************/
    /* Navigation Tests */
    /**************************************************************/
    /**************************************************************/
    /**
     * Runs a scoped legacy DOM assertion for the static-site smoke suite.
     *
     * @private
     */
    /**************************************************************/
    function testNavigation() {
        var navbar = document.querySelector('.navbar');
        assert('Navbar exists', !!navbar, 'Expected .navbar element');

        var navLinks = document.querySelectorAll('.navbar-menu a');
        assert('Navbar has links', navLinks.length >= 4, 'Expected at least 4 nav links, found ' + navLinks.length);

        var linkTexts = Array.from(navLinks).map(function (a) { return a.textContent.trim(); });
        assert('Has Home link', linkTexts.some(function (t) { return t.includes('Home'); }), 'Missing Home nav link');
        assert('Has Ai link', linkTexts.some(function (t) { return t.includes('Ai'); }), 'Missing Ai nav link');
        assert('Has MCP link', linkTexts.some(function (t) { return t.includes('MCP'); }), 'Missing MCP nav link');
        assert('Has API Docs link', linkTexts.some(function (t) { return t.includes('API Docs'); }), 'Missing API Docs nav link');
    }

    /**************************************************************/
    /* Footer Tests */
    /**************************************************************/
    /**************************************************************/
    /**
     * Runs a scoped legacy DOM assertion for the static-site smoke suite.
     *
     * @private
     */
    /**************************************************************/
    function testFooter() {
        var footer = document.querySelector('footer');
        assert('Footer exists', !!footer, 'Expected footer element');

        var footerLinks = document.querySelectorAll('.footer-column-links a');
        assert('Footer has links', footerLinks.length >= 6, 'Expected at least 6 footer links, found ' + footerLinks.length);

        var footerTexts = Array.from(footerLinks).map(function (a) { return a.textContent.trim(); });
        assert('Footer has MCP Docs', footerTexts.some(function (t) { return t.includes('MCP Docs'); }), 'Missing MCP Docs footer link');
        assert('Footer has Getting Started', footerTexts.some(function (t) { return t.includes('Getting Started'); }), 'Missing Getting Started footer link');
        assert('Footer has Terms', footerTexts.some(function (t) { return t.includes('Terms'); }), 'Missing Terms footer link');
        assert('Footer has Privacy', footerTexts.some(function (t) { return t.includes('Privacy'); }), 'Missing Privacy footer link');
    }

    /**************************************************************/
    /* MCP Docs Page Tests */
    /**************************************************************/
    /**************************************************************/
    /**
     * Runs a scoped legacy DOM assertion for the static-site smoke suite.
     *
     * @private
     */
    /**************************************************************/
    function testMcpDocsPage() {
        var mcpPage = document.querySelector('.mcp-page');
        if (!mcpPage) {
            assert('MCP page detected', false, 'Not on an MCP page â€” skipping MCP-specific tests');
            return;
        }

        assert('MCP page container exists', true);

        var toolCards = document.querySelectorAll('.tool-card');
        assert('Has tool cards', toolCards.length > 0, 'Expected at least 1 tool card, found ' + toolCards.length);

        var toolNames = document.querySelectorAll('.tool-name');
        var names = Array.from(toolNames).map(function (el) { return el.textContent.trim(); });
        assert('Has search_drug_labels', names.includes('search_drug_labels'), 'Missing search_drug_labels tool');
        assert('Has search_by_pharmacologic_class', names.includes('search_by_pharmacologic_class'), 'Missing search_by_pharmacologic_class tool');
        assert('Has search_by_indication', names.includes('search_by_indication'), 'Missing search_by_indication tool');

        var tables = document.querySelectorAll('.mcp-table');
        assert('Has data tables', tables.length > 0, 'Expected at least 1 .mcp-table');
    }

    /**************************************************************/
    /* MCP Setup Page Tests */
    /**************************************************************/
    /**************************************************************/
    /**
     * Runs a scoped legacy DOM assertion for the static-site smoke suite.
     *
     * @private
     */
    /**************************************************************/
    function testMcpSetupPage() {
        var featureGrid = document.querySelector('.feature-grid-mcp');
        if (!featureGrid) return; // Not on setup page

        var features = document.querySelectorAll('.feature-item-mcp');
        assert('Has feature cards', features.length >= 7, 'Expected at least 7 feature cards, found ' + features.length);

        var steps = document.querySelectorAll('.step-counter li');
        assert('Has getting started steps', steps.length >= 4, 'Expected at least 4 steps, found ' + steps.length);

        var examples = document.querySelectorAll('.example-card');
        assert('Has example cards', examples.length >= 6, 'Expected at least 6 example cards, found ' + examples.length);

        var screenshots = document.querySelectorAll('.example-screenshot img');
        assert('Has screenshots', screenshots.length >= 6, 'Expected at least 6 screenshots, found ' + screenshots.length);
    }

    /**************************************************************/
    /* Chat Page Tests */
    /**************************************************************/
    /**************************************************************/
    /**
     * Runs a scoped legacy DOM assertion for the static-site smoke suite.
     *
     * @private
     */
    /**************************************************************/
    function testChatPage() {
        var chatPage = document.querySelector('.chat-page');
        if (!chatPage) return; // Not on chat page

        assert('Chat page container exists', true);

        var subheader = document.querySelector('.chat-subheader');
        assert('Chat subheader exists', !!subheader, 'Expected .chat-subheader element');

        var messagesContainer = document.querySelector('#messagesContainer');
        assert('Messages container exists', !!messagesContainer, 'Expected #messagesContainer');

        var inputField = document.querySelector('#messageInput');
        assert('Input field exists', !!inputField, 'Expected #messageInput');

        // Check earth-tone theme applied
        var computed = getComputedStyle(document.documentElement);
        var accent = computed.getPropertyValue('--color-accent').trim();
        assert('Earth-tone accent applied', accent === '#e5771e', 'Expected --color-accent=#e5771e, got ' + accent);
    }

    /**************************************************************/
    /* Scroll Animation Tests */
    /**************************************************************/
    /**************************************************************/
    /**
     * Runs a scoped legacy DOM assertion for the static-site smoke suite.
     *
     * @private
     */
    /**************************************************************/
    function testScrollAnimations() {
        var animElements = document.querySelectorAll('.animate-on-scroll');
        if (animElements.length === 0) return; // No animations on this page

        assert('Has animatable elements', animElements.length > 0, 'Expected .animate-on-scroll elements');
    }

    /**************************************************************/
    /* Navbar Scroll Behavior Tests */
    /**************************************************************/
    /**************************************************************/
    /**
     * Runs a scoped legacy DOM assertion for the static-site smoke suite.
     *
     * @private
     */
    /**************************************************************/
    function testNavbarScrollBehavior() {
        var navbar = document.querySelector('.navbar');
        if (!navbar) return;

        assert('Navbar exists for scroll test', true);
        // Navbar hidden class should not be present initially
        assert('Navbar not hidden initially', !navbar.classList.contains('navbar--hidden'));
    }

    /**************************************************************/
    /* Test Runner */
    /**************************************************************/
    /**************************************************************/
    /**
     * Runs a scoped legacy DOM assertion for the static-site smoke suite.
     *
     * @public
     */
    /**************************************************************/
    function runAll() {
        results = { passed: 0, failed: 0, tests: [] };

        testNavigation();
        testFooter();
        testMcpDocsPage();
        testMcpSetupPage();
        testChatPage();
        testScrollAnimations();
        testNavbarScrollBehavior();

        // Output results
        console.group('MedRecPro Site Tests');
        console.log('Passed: ' + results.passed + '/' + (results.passed + results.failed));

        results.tests.forEach(function (t) {
            if (t.passed) {
                console.log('%c  PASS  %s', 'color: green', t.name);
            } else {
                console.log('%c  FAIL  %s: %s', 'color: red; font-weight: bold', t.name, t.message);
            }
        });

        console.groupEnd();
        return results;
    }

    /**************************************************************/
    /**
     * Runs one bounded UI smoke-test category without changing the legacy aggregate suite.
     *
     * @param {string} category Named UI category: all, navigation, footer, behavior, chat, mcp-docs, or mcp-setup.
     * @returns {Object} Fresh category result with named not-applicable outcomes for page-specific categories.
     *
     * @remarks
     * The slash-command router uses this entry point so a command never navigates the user merely to
     * satisfy a page-specific assertion. The established runAll() behavior is intentionally preserved.
     */
    /**************************************************************/
    function runUiTests(category) {
        var selected = String(category || 'all').toLowerCase();
        var knownCategories = ['all', 'navigation', 'footer', 'behavior', 'chat', 'mcp-docs', 'mcp-setup'];
        if (knownCategories.indexOf(selected) < 0) {
            return createNotApplicableResult(selected, 'one of: ' + knownCategories.slice(1).join(', ') + '.');
        }

        var requires = {
            chat: '.chat-page',
            'mcp-docs': '.mcp-page',
            'mcp-setup': '.feature-grid-mcp'
        };
        if (requires[selected] && !document.querySelector(requires[selected])) {
            return createNotApplicableResult(selected, 'the page containing ' + requires[selected] + '.');
        }

        results = { passed: 0, failed: 0, tests: [] };
        if (selected === 'all' || selected === 'navigation') testNavigation();
        if (selected === 'all' || selected === 'footer') testFooter();
        if (selected === 'all' || selected === 'behavior') {
            testScrollAnimations();
            testNavbarScrollBehavior();
        }
        if ((selected === 'all' && document.querySelector('.chat-page')) || selected === 'chat') testChatPage();
        if ((selected === 'all' && document.querySelector('.mcp-page')) || selected === 'mcp-docs') testMcpDocsPage();
        if ((selected === 'all' && document.querySelector('.feature-grid-mcp')) || selected === 'mcp-setup') testMcpSetupPage();

        var total = results.passed + results.failed;
        return {
            category: selected,
            passed: results.passed,
            failed: results.failed,
            skipped: 0,
            total: total,
            tests: results.tests.slice(),
            summary: 'UI ' + selected + ': ' + results.passed + '/' + total + ' passed.'
        };
    }

    /**************************************************************/
    /**
     * Creates a visible, non-failing result for a UI category that requires another MVC page.
     *
     * @param {string} category Requested UI category.
     * @param {string} requirement Required page selector or supported-category guidance.
     * @returns {Object} Not-applicable category report.
     */
    /**************************************************************/
    function createNotApplicableResult(category, requirement) {
        var message = 'Not applicable on this page; requires ' + requirement;
        return {
            category: category,
            passed: 0,
            failed: 0,
            skipped: 1,
            total: 0,
            notApplicable: true,
            tests: [{ name: 'UI ' + category, passed: null, skipped: true, message: message }],
            summary: 'UI ' + category + ': SKIP - ' + message
        };
    }

    return { runAll: runAll, runUiTests: runUiTests };
})();
