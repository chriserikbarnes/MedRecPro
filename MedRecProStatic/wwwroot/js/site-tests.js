/* ============================================================
   MedRecPro Site Tests
   DOM-based tests for navigation, MCP pages, and interactions.
   Run via browser console: MedRecProTests.runAll()
   ============================================================ */

var MedRecProTests = (function () {
    'use strict';

    var results = { passed: 0, failed: 0, tests: [] };

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
    function testMcpDocsPage() {
        var mcpPage = document.querySelector('.mcp-page');
        if (!mcpPage) {
            assert('MCP page detected', false, 'Not on an MCP page — skipping MCP-specific tests');
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
    function testScrollAnimations() {
        var animElements = document.querySelectorAll('.animate-on-scroll');
        if (animElements.length === 0) return; // No animations on this page

        assert('Has animatable elements', animElements.length > 0, 'Expected .animate-on-scroll elements');
    }

    /**************************************************************/
    /* Navbar Scroll Behavior Tests */
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

    return { runAll: runAll };
})();
