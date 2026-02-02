/**************************************************************/
/**
 * MedRecPro Chat Test Framework Module
 *
 * @fileoverview Provides a comprehensive test framework for validating
 * the functionality of all JavaScript modules in the chat interface.
 *
 * @description
 * The test framework module provides:
 * - Unit tests for utility functions (utils.js)
 * - Unit tests for markdown rendering (markdown.js)
 * - Unit tests for state management (state.js)
 * - Unit tests for result grouping (result-grouper.js)
 * - Unit tests for configuration (config.js)
 * - Integration test hooks for API service
 * - Test runner with detailed reporting
 *
 * Tests are designed to be run from the chat interface using the /test command.
 *
 * @example
 * import { TestRunner } from './util-test.js';
 *
 * // Run all tests
 * const results = await TestRunner.runAllTests();
 *
 * // Run specific module tests
 * const utilResults = TestRunner.runUtilsTests();
 *
 * @module chat/util-test
 * @see ChatUtils - Utility functions being tested
 * @see MarkdownRenderer - Markdown module being tested
 * @see ChatState - State module being tested
 * @see ResultGrouper - Result grouper module being tested
 */
/**************************************************************/

import { ChatUtils } from './utils.js';
import { ChatState } from './state.js';
import { MarkdownRenderer } from './markdown.js';
import { ResultGrouper } from './result-grouper.js';
import { ChatConfig } from './config.js';

export const TestRunner = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Test result storage.
     *
     * @type {Object}
     * @property {number} passed - Count of passed tests
     * @property {number} failed - Count of failed tests
     * @property {number} total - Total test count
     * @property {Array} results - Detailed test results
     */
    /**************************************************************/
    let testResults = {
        passed: 0,
        failed: 0,
        total: 0,
        results: []
    };

    /**************************************************************/
    /**
     * Test data loaded from test.json.
     *
     * @type {Object|null}
     */
    /**************************************************************/
    let testData = null;

    /**************************************************************/
    /**
     * Resets test results for a fresh run.
     */
    /**************************************************************/
    function resetResults() {
        testResults = {
            passed: 0,
            failed: 0,
            total: 0,
            results: []
        };
    }

    /**************************************************************/
    /**
     * Records a test result.
     *
     * @param {string} moduleName - Name of the module being tested
     * @param {string} testName - Name of the specific test
     * @param {boolean} passed - Whether the test passed
     * @param {string} [message=''] - Optional message or error details
     * @param {*} [expected] - Expected value
     * @param {*} [actual] - Actual value received
     */
    /**************************************************************/
    function recordResult(moduleName, testName, passed, message = '', expected = undefined, actual = undefined) {
        testResults.total++;
        if (passed) {
            testResults.passed++;
        } else {
            testResults.failed++;
            // Log failed tests immediately for debugging
            console.warn(`[TestRunner] FAILED: ${moduleName} - ${testName}: ${message}`);
            if (expected !== undefined) {
                console.warn(`[TestRunner]   Expected: ${expected}`);
            }
            if (actual !== undefined) {
                console.warn(`[TestRunner]   Actual: ${actual}`);
            }
        }

        testResults.results.push({
            module: moduleName,
            test: testName,
            passed: passed,
            message: message,
            expected: expected,
            actual: actual
        });
    }

    /**************************************************************/
    /**
     * Assertion helper: checks if two values are equal.
     *
     * @param {*} actual - Actual value
     * @param {*} expected - Expected value
     * @param {string} moduleName - Module name for reporting
     * @param {string} testName - Test name for reporting
     * @returns {boolean} Whether assertion passed
     */
    /**************************************************************/
    function assertEqual(actual, expected, moduleName, testName) {
        const passed = actual === expected;
        recordResult(
            moduleName,
            testName,
            passed,
            passed ? 'Passed' : `Expected "${expected}" but got "${actual}"`,
            expected,
            actual
        );
        return passed;
    }

    /**************************************************************/
    /**
     * Assertion helper: checks if value is truthy.
     *
     * @param {*} value - Value to check
     * @param {string} moduleName - Module name for reporting
     * @param {string} testName - Test name for reporting
     * @returns {boolean} Whether assertion passed
     */
    /**************************************************************/
    function assertTrue(value, moduleName, testName) {
        const passed = Boolean(value);
        recordResult(
            moduleName,
            testName,
            passed,
            passed ? 'Passed' : `Expected truthy value but got "${value}"`
        );
        return passed;
    }

    /**************************************************************/
    /**
     * Assertion helper: checks if value is falsy.
     *
     * @param {*} value - Value to check
     * @param {string} moduleName - Module name for reporting
     * @param {string} testName - Test name for reporting
     * @returns {boolean} Whether assertion passed
     */
    /**************************************************************/
    function assertFalse(value, moduleName, testName) {
        const passed = !Boolean(value);
        recordResult(
            moduleName,
            testName,
            passed,
            passed ? 'Passed' : `Expected falsy value but got "${value}"`
        );
        return passed;
    }

    /**************************************************************/
    /**
     * Assertion helper: checks if value matches a regex pattern.
     *
     * @param {string} value - Value to test
     * @param {RegExp} pattern - Regex pattern to match
     * @param {string} moduleName - Module name for reporting
     * @param {string} testName - Test name for reporting
     * @returns {boolean} Whether assertion passed
     */
    /**************************************************************/
    function assertMatches(value, pattern, moduleName, testName) {
        const passed = pattern.test(value);
        recordResult(
            moduleName,
            testName,
            passed,
            passed ? 'Passed' : `Value "${value}" does not match pattern ${pattern}`,
            pattern.toString(),
            value
        );
        return passed;
    }

    /**************************************************************/
    /**
     * Assertion helper: checks if a function throws an error.
     *
     * @param {Function} fn - Function to execute
     * @param {string} moduleName - Module name for reporting
     * @param {string} testName - Test name for reporting
     * @returns {boolean} Whether function threw
     */
    /**************************************************************/
    function assertThrows(fn, moduleName, testName) {
        let threw = false;
        try {
            fn();
        } catch (e) {
            threw = true;
        }
        recordResult(
            moduleName,
            testName,
            threw,
            threw ? 'Passed - function threw as expected' : 'Expected function to throw but it did not'
        );
        return threw;
    }

    /**************************************************************/
    /**
     * Assertion helper: checks if a function does not throw.
     *
     * @param {Function} fn - Function to execute
     * @param {string} moduleName - Module name for reporting
     * @param {string} testName - Test name for reporting
     * @returns {boolean} Whether function did not throw
     */
    /**************************************************************/
    function assertNoThrow(fn, moduleName, testName) {
        let threw = false;
        let errorMsg = '';
        try {
            fn();
        } catch (e) {
            threw = true;
            errorMsg = e.message;
        }
        recordResult(
            moduleName,
            testName,
            !threw,
            !threw ? 'Passed' : `Function threw unexpected error: ${errorMsg}`
        );
        return !threw;
    }

    /**************************************************************/
    /**
     * Assertion helper: deep equality check for objects and arrays.
     *
     * @param {*} actual - Actual value
     * @param {*} expected - Expected value
     * @param {string} moduleName - Module name for reporting
     * @param {string} testName - Test name for reporting
     * @returns {boolean} Whether assertion passed
     */
    /**************************************************************/
    function assertDeepEqual(actual, expected, moduleName, testName) {
        const actualStr = JSON.stringify(actual);
        const expectedStr = JSON.stringify(expected);
        const passed = actualStr === expectedStr;
        recordResult(
            moduleName,
            testName,
            passed,
            passed ? 'Passed' : `Deep equality failed`,
            expectedStr,
            actualStr
        );
        return passed;
    }

    /**************************************************************/
    /**
     * Safely runs a test function, catching any exceptions.
     *
     * @param {Function} testFn - Test function to execute
     * @param {string} moduleName - Module name for reporting
     * @param {string} testName - Test name for reporting
     * @returns {boolean} Whether test executed without throwing
     *
     * @description
     * Wraps test execution in try-catch to prevent individual test
     * failures from halting the entire test suite.
     */
    /**************************************************************/
    function safeTest(testFn, moduleName, testName) {
        try {
            testFn();
            return true;
        } catch (error) {
            recordResult(
                moduleName,
                testName,
                false,
                `Test threw an exception: ${error.message}`
            );
            console.error(`[TestRunner] Test "${testName}" threw:`, error);
            return false;
        }
    }

    /**************************************************************/
    /**
     * Safely runs multiple test cases from test data array.
     *
     * @param {Array} testCases - Array of test case objects
     * @param {Function} testFn - Function to run for each test case (receives test, index)
     * @param {string} moduleName - Module name for reporting
     * @param {string} testBaseName - Base name for test (index will be appended)
     */
    /**************************************************************/
    function runTestCases(testCases, testFn, moduleName, testBaseName) {
        if (!testCases || !Array.isArray(testCases)) return;

        testCases.forEach((test, i) => {
            const testName = `${testBaseName} - test case ${i + 1}`;
            safeTest(() => testFn(test, i), moduleName, testName);
        });
    }

    /**************************************************************/
    /**
     * Loads test data from test.json file.
     *
     * @returns {Promise<Object>} Test data object
     */
    /**************************************************************/
    async function loadTestData() {
        if (testData) {
            return testData;
        }

        try {
            const response = await fetch('/lib/test.json');
            if (!response.ok) {
                throw new Error(`Failed to load test.json: ${response.status}`);
            }
            testData = await response.json();
            return testData;
        } catch (error) {
            console.error('[TestRunner] Failed to load test data:', error);
            // Return default test data if file not found
            return getDefaultTestData();
        }
    }

    /**************************************************************/
    /**
     * Returns default test data if test.json cannot be loaded.
     *
     * @returns {Object} Default test data
     */
    /**************************************************************/
    function getDefaultTestData() {
        return {
            utils: {
                escapeHtml: [
                    { input: '<script>alert("xss")</script>', expected: '&lt;script&gt;alert("xss")&lt;/script&gt;' },
                    { input: 'Normal text', expected: 'Normal text' },
                    { input: '<div class="test">', expected: '&lt;div class="test"&gt;' }
                ],
                formatFileSize: [
                    { input: 512, expected: '512 B' },
                    { input: 1536, expected: '1.5 KB' },
                    { input: 5242880, expected: '5.0 MB' }
                ],
                toTitleCase: [
                    { input: 'LISINOPRIL', expected: 'Lisinopril' },
                    { input: 'metformin hcl', expected: 'Metformin HCL' },
                    { input: 'METOPROLOL SUCCINATE ER', expected: 'Metoprolol Succinate ER' }
                ],
                truncate: [
                    { input: { str: 'This is a long string', maxLength: 10 }, expected: 'This is...' },
                    { input: { str: 'Short', maxLength: 10 }, expected: 'Short' }
                ],
                isNonEmptyString: [
                    { input: 'hello', expected: true },
                    { input: '', expected: false },
                    { input: null, expected: false },
                    { input: 123, expected: false }
                ],
                safeParseJSON: [
                    { input: { json: '{"valid": true}', fallback: {} }, expected: { valid: true } },
                    { input: { json: 'invalid', fallback: {} }, expected: {} }
                ]
            },
            markdown: {
                render: [
                    { input: '**Bold**', contains: '<strong>Bold</strong>' },
                    { input: '*Italic*', contains: '<em>Italic</em>' },
                    { input: '`code`', contains: '<code>code</code>' },
                    { input: '# Header', contains: '<h1>Header</h1>' },
                    { input: '## Header 2', contains: '<h2>Header 2</h2>' },
                    { input: '- item', contains: '<li>item</li>' }
                ]
            },
            resultGrouper: {
                mockResults: [
                    {
                        statusCode: 200,
                        result: { documentGuid: 'abc-123', productName: 'Lisinopril' },
                        specification: { description: 'Test endpoint' }
                    },
                    {
                        statusCode: 200,
                        result: { documentGuid: 'def-456', productName: 'Metformin' },
                        specification: { description: 'Test endpoint 2' }
                    }
                ]
            }
        };
    }

    /**************************************************************/
    /**
     * Runs all ChatUtils module tests.
     *
     * @param {Object} data - Test data for utils tests
     * @returns {Object} Test results for this module
     */
    /**************************************************************/
    function runUtilsTests(data) {
        const moduleName = 'ChatUtils';
        const moduleResults = { passed: 0, failed: 0, tests: [] };

        // Test generateUUID
        const uuid = ChatUtils.generateUUID();
        assertMatches(
            uuid,
            /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
            moduleName,
            'generateUUID - valid UUID v4 format'
        );

        // Test UUID uniqueness
        const uuid2 = ChatUtils.generateUUID();
        assertTrue(uuid !== uuid2, moduleName, 'generateUUID - generates unique IDs');

        // Test escapeHtml with test data (using safe wrapper)
        runTestCases(data?.utils?.escapeHtml, (test) => {
            const result = ChatUtils.escapeHtml(test.input);
            assertEqual(result, test.expected, moduleName, `escapeHtml - ${test.description || 'test'}`);
        }, moduleName, 'escapeHtml');

        // Test escapeHtml edge cases
        safeTest(() => assertEqual(ChatUtils.escapeHtml(''), '', moduleName, 'escapeHtml - empty string'), moduleName, 'escapeHtml - empty string');
        safeTest(() => assertEqual(ChatUtils.escapeHtml('&'), '&amp;', moduleName, 'escapeHtml - ampersand'), moduleName, 'escapeHtml - ampersand');

        // Test formatFileSize with test data (using safe wrapper)
        runTestCases(data?.utils?.formatFileSize, (test) => {
            const result = ChatUtils.formatFileSize(test.input);
            assertEqual(result, test.expected, moduleName, `formatFileSize - ${test.description || 'test'}`);
        }, moduleName, 'formatFileSize');

        // Test formatFileSize edge cases
        safeTest(() => assertEqual(ChatUtils.formatFileSize(0), '0 B', moduleName, 'formatFileSize - zero bytes'), moduleName, 'formatFileSize - zero bytes');
        safeTest(() => assertEqual(ChatUtils.formatFileSize(1024), '1.0 KB', moduleName, 'formatFileSize - exactly 1 KB'), moduleName, 'formatFileSize - exactly 1 KB');
        safeTest(() => assertEqual(ChatUtils.formatFileSize(1048576), '1.0 MB', moduleName, 'formatFileSize - exactly 1 MB'), moduleName, 'formatFileSize - exactly 1 MB');

        // Test toTitleCase with test data (using safe wrapper)
        runTestCases(data?.utils?.toTitleCase, (test) => {
            const result = ChatUtils.toTitleCase(test.input);
            assertEqual(result, test.expected, moduleName, `toTitleCase - ${test.description || 'test'}`);
        }, moduleName, 'toTitleCase');

        // Test toTitleCase edge cases
        safeTest(() => assertEqual(ChatUtils.toTitleCase(''), '', moduleName, 'toTitleCase - empty string'), moduleName, 'toTitleCase - empty string');
        safeTest(() => assertEqual(ChatUtils.toTitleCase(null), '', moduleName, 'toTitleCase - null input'), moduleName, 'toTitleCase - null input');
        safeTest(() => assertEqual(ChatUtils.toTitleCase('omega-3 fatty acids'), 'Omega-3 Fatty Acids', moduleName, 'toTitleCase - hyphenated'), moduleName, 'toTitleCase - hyphenated');

        // Test truncate with test data (using safe wrapper)
        runTestCases(data?.utils?.truncate, (test) => {
            const result = ChatUtils.truncate(test.input.str, test.input.maxLength);
            assertEqual(result, test.expected, moduleName, `truncate - ${test.description || 'test'}`);
        }, moduleName, 'truncate');

        // Test truncate edge cases
        safeTest(() => assertEqual(ChatUtils.truncate('', 10), '', moduleName, 'truncate - empty string'), moduleName, 'truncate - empty string');
        safeTest(() => assertEqual(ChatUtils.truncate(null, 10), null, moduleName, 'truncate - null input'), moduleName, 'truncate - null input');

        // Test isNonEmptyString with test data (using safe wrapper)
        runTestCases(data?.utils?.isNonEmptyString, (test) => {
            const result = ChatUtils.isNonEmptyString(test.input);
            assertEqual(result, test.expected, moduleName, `isNonEmptyString - ${test.description || 'test'}`);
        }, moduleName, 'isNonEmptyString');

        // Test isNonEmptyString edge cases
        safeTest(() => assertFalse(ChatUtils.isNonEmptyString('   '), moduleName, 'isNonEmptyString - whitespace only'), moduleName, 'isNonEmptyString - whitespace only');
        safeTest(() => assertTrue(ChatUtils.isNonEmptyString('a'), moduleName, 'isNonEmptyString - single char'), moduleName, 'isNonEmptyString - single char');

        // Test safeParseJSON with test data (using safe wrapper)
        runTestCases(data?.utils?.safeParseJSON, (test) => {
            // Ensure test.input has proper structure before calling
            if (test.input && typeof test.input.json === 'string') {
                const result = ChatUtils.safeParseJSON(test.input.json, test.input.fallback);
                assertDeepEqual(result, test.expected, moduleName, `safeParseJSON - ${test.description || 'test'}`);
            } else {
                // Skip malformed test cases but log them
                console.warn(`[TestRunner] Skipping malformed safeParseJSON test: ${test.description}`);
            }
        }, moduleName, 'safeParseJSON');

        // Test deepClone
        safeTest(() => {
            const original = { nested: { value: 1 }, arr: [1, 2, 3] };
            const clone = ChatUtils.deepClone(original);
            assertDeepEqual(clone, original, moduleName, 'deepClone - creates equal copy');
            clone.nested.value = 999;
            assertEqual(original.nested.value, 1, moduleName, 'deepClone - original unchanged after modifying clone');
        }, moduleName, 'deepClone');

        // Test debounce (basic functionality check)
        safeTest(() => {
            assertNoThrow(() => {
                const debounced = ChatUtils.debounce(() => {}, 100);
                debounced();
            }, moduleName, 'debounce - creates callable function');
        }, moduleName, 'debounce');

        return moduleResults;
    }

    /**************************************************************/
    /**
     * Runs all MarkdownRenderer module tests.
     *
     * @param {Object} data - Test data for markdown tests
     * @returns {Object} Test results for this module
     */
    /**************************************************************/
    function runMarkdownTests(data) {
        const moduleName = 'MarkdownRenderer';

        // Test render function exists
        safeTest(() => assertTrue(typeof MarkdownRenderer.render === 'function', moduleName, 'render function exists'), moduleName, 'render function exists');

        // Test basic rendering with test data (using safe wrapper)
        runTestCases(data?.markdown?.render, (test) => {
            const result = MarkdownRenderer.render(test.input);
            assertTrue(
                result.includes(test.contains),
                moduleName,
                `render - ${test.description || test.input}`
            );
        }, moduleName, 'render');

        // Test render edge cases
        safeTest(() => assertEqual(MarkdownRenderer.render(''), '', moduleName, 'render - empty string'), moduleName, 'render - empty string');
        safeTest(() => assertEqual(MarkdownRenderer.render(null), '', moduleName, 'render - null input'), moduleName, 'render - null input');
        safeTest(() => assertEqual(MarkdownRenderer.render(undefined), '', moduleName, 'render - undefined input'), moduleName, 'render - undefined input');

        // Test links with API URLs
        safeTest(() => {
            const linkResult = MarkdownRenderer.render('[Test](/api/test)');
            assertTrue(
                linkResult.includes('href=') && linkResult.includes('/api/test'),
                moduleName,
                'render - handles API links'
            );
        }, moduleName, 'render - handles API links');

        // Test code blocks
        safeTest(() => {
            const codeResult = MarkdownRenderer.render('```js\nconst x = 1;\n```');
            assertTrue(codeResult.includes('code-block'), moduleName, 'render - creates code block wrapper');
            assertTrue(codeResult.includes('const x = 1'), moduleName, 'render - preserves code content');
        }, moduleName, 'render - code blocks');

        // Test horizontal rule
        safeTest(() => {
            const hrResult = MarkdownRenderer.render('---');
            assertTrue(hrResult.includes('<hr>'), moduleName, 'render - horizontal rule');
        }, moduleName, 'render - horizontal rule');

        // Test blockquote (escaped)
        safeTest(() => {
            const bqResult = MarkdownRenderer.render('> quote text');
            assertTrue(bqResult.includes('blockquote'), moduleName, 'render - blockquote');
        }, moduleName, 'render - blockquote');

        // Test numbered list
        safeTest(() => {
            const numListResult = MarkdownRenderer.render('1. First item');
            assertTrue(numListResult.includes('1.'), moduleName, 'render - numbered list item');
        }, moduleName, 'render - numbered list item');

        // Test copyCode function exists
        safeTest(() => assertTrue(typeof MarkdownRenderer.copyCode === 'function', moduleName, 'copyCode function exists'), moduleName, 'copyCode function exists');

        // Test clearCodeBlockStorage function exists
        safeTest(() => assertTrue(
            typeof MarkdownRenderer.clearCodeBlockStorage === 'function',
            moduleName,
            'clearCodeBlockStorage function exists'
        ), moduleName, 'clearCodeBlockStorage function exists');

        // Test pruneConsecutiveHorizontalRules
        const prunedHr = MarkdownRenderer.pruneConsecutiveHorizontalRules('<hr><hr>');
        assertEqual(prunedHr, '<hr>', moduleName, 'pruneConsecutiveHorizontalRules - collapses consecutive');

        return { passed: 0, failed: 0, tests: [] };
    }

    /**************************************************************/
    /**
     * Runs all ChatState module tests.
     *
     * @returns {Object} Test results for this module
     */
    /**************************************************************/
    function runStateTests() {
        const moduleName = 'ChatState';

        // Save current state to restore later (deep clone to preserve)
        const originalMessages = ChatUtils.deepClone(ChatState.getMessages());
        const originalLoading = ChatState.isLoading();
        const originalShowFileUpload = ChatState.isShowFileUpload();

        // Test message operations
        ChatState.clearConversation();
        assertDeepEqual(ChatState.getMessages(), [], moduleName, 'clearConversation - empties messages');

        // Test addMessage
        const msg = ChatState.addMessage({ role: 'user', content: 'Test message' });
        assertTrue(msg.id !== undefined, moduleName, 'addMessage - generates ID');
        assertTrue(msg.timestamp !== undefined, moduleName, 'addMessage - adds timestamp');
        assertEqual(msg.content, 'Test message', moduleName, 'addMessage - preserves content');

        // Test getMessages
        const messages = ChatState.getMessages();
        assertEqual(messages.length, 1, moduleName, 'getMessages - returns messages array');

        // Test getMessageById
        const foundMsg = ChatState.getMessageById(msg.id);
        assertEqual(foundMsg.content, 'Test message', moduleName, 'getMessageById - finds message');

        // Test getMessageById with invalid ID
        const notFound = ChatState.getMessageById('invalid-id');
        assertEqual(notFound, undefined, moduleName, 'getMessageById - returns undefined for invalid ID');

        // Test updateMessage
        ChatState.updateMessage(msg.id, { content: 'Updated message' });
        const updatedMsg = ChatState.getMessageById(msg.id);
        assertEqual(updatedMsg.content, 'Updated message', moduleName, 'updateMessage - updates content');

        // Test removeMessage
        const removed = ChatState.removeMessage(msg.id);
        assertTrue(removed, moduleName, 'removeMessage - returns true on success');
        assertEqual(ChatState.getMessages().length, 0, moduleName, 'removeMessage - removes message');

        // Test loading state
        ChatState.setLoading(true);
        assertTrue(ChatState.isLoading(), moduleName, 'setLoading/isLoading - true');
        ChatState.setLoading(false);
        assertFalse(ChatState.isLoading(), moduleName, 'setLoading/isLoading - false');

        // Test file operations
        ChatState.clearFiles();
        assertEqual(ChatState.getFiles().length, 0, moduleName, 'clearFiles - empties files');
        assertEqual(ChatState.getFileCount(), 0, moduleName, 'getFileCount - returns 0');

        // Test file upload visibility
        ChatState.setShowFileUpload(true);
        assertTrue(ChatState.isShowFileUpload(), moduleName, 'setShowFileUpload/isShowFileUpload - true');
        ChatState.setShowFileUpload(false);
        assertFalse(ChatState.isShowFileUpload(), moduleName, 'setShowFileUpload/isShowFileUpload - false');

        // Test conversation ID
        const convId = ChatState.getConversationId();
        assertTrue(convId !== undefined && convId.length > 0, moduleName, 'getConversationId - returns ID');

        // Test checkpoint state
        ChatState.clearCheckpointState();
        assertEqual(ChatState.getCheckpointState(), null, moduleName, 'clearCheckpointState - clears state');
        assertFalse(ChatState.hasActiveCheckpoint(), moduleName, 'hasActiveCheckpoint - false when no checkpoint');

        ChatState.setCheckpointState({ messageId: 'test', status: 'pending' });
        assertTrue(ChatState.hasActiveCheckpoint(), moduleName, 'hasActiveCheckpoint - true when pending');
        ChatState.clearCheckpointState();

        // Test progress items
        ChatState.clearProgressItems();
        assertEqual(ChatState.getProgressItemCount(), 0, moduleName, 'clearProgressItems - empties items');

        ChatState.addProgressItem({ name: 'Test', success: true });
        assertEqual(ChatState.getProgressItemCount(), 1, moduleName, 'addProgressItem - adds item');

        const items = ChatState.getProgressItems();
        assertTrue(items[0].timestamp !== undefined, moduleName, 'addProgressItem - adds timestamp');
        ChatState.clearProgressItems();

        // Restore original state - IMPORTANT: must restore messages so test command can display results
        ChatState.clearConversation();
        originalMessages.forEach(msg => ChatState.addMessage(msg));
        ChatState.setLoading(originalLoading);
        ChatState.setShowFileUpload(originalShowFileUpload);

        return { passed: 0, failed: 0, tests: [] };
    }

    /**************************************************************/
    /**
     * Runs all ResultGrouper module tests.
     *
     * @param {Object} data - Test data for result grouper tests
     * @returns {Object} Test results for this module
     */
    /**************************************************************/
    function runResultGrouperTests(data) {
        const moduleName = 'ResultGrouper';

        // Test groupResultsByProduct function exists
        assertTrue(
            typeof ResultGrouper.groupResultsByProduct === 'function',
            moduleName,
            'groupResultsByProduct function exists'
        );

        // Test with empty input
        const emptyResult = ResultGrouper.groupResultsByProduct([]);
        assertDeepEqual(emptyResult, {}, moduleName, 'groupResultsByProduct - empty array');

        // Test with null input
        const nullResult = ResultGrouper.groupResultsByProduct(null);
        assertDeepEqual(nullResult, {}, moduleName, 'groupResultsByProduct - null input');

        // Test with mock results
        if (data?.resultGrouper?.mockResults) {
            const groups = ResultGrouper.groupResultsByProduct(data.resultGrouper.mockResults);
            assertTrue(Object.keys(groups).length > 0, moduleName, 'groupResultsByProduct - creates groups');
        }

        // Test getProductCount
        assertTrue(
            typeof ResultGrouper.getProductCount === 'function',
            moduleName,
            'getProductCount function exists'
        );
        assertEqual(ResultGrouper.getProductCount({}), 0, moduleName, 'getProductCount - empty object');
        assertEqual(ResultGrouper.getProductCount(null), 0, moduleName, 'getProductCount - null input');

        // Test hasResultData
        assertTrue(
            typeof ResultGrouper.hasResultData === 'function',
            moduleName,
            'hasResultData function exists'
        );
        assertFalse(ResultGrouper.hasResultData(null), moduleName, 'hasResultData - null input');
        assertFalse(ResultGrouper.hasResultData({ statusCode: 404 }), moduleName, 'hasResultData - error status');
        assertTrue(
            ResultGrouper.hasResultData({ statusCode: 200, result: { data: 'test' } }),
            moduleName,
            'hasResultData - valid result'
        );

        // Test getResultDataSize
        assertTrue(
            typeof ResultGrouper.getResultDataSize === 'function',
            moduleName,
            'getResultDataSize function exists'
        );
        assertEqual(ResultGrouper.getResultDataSize(null), 0, moduleName, 'getResultDataSize - null input');
        assertTrue(
            ResultGrouper.getResultDataSize({ result: { test: 'data' } }) > 0,
            moduleName,
            'getResultDataSize - returns positive for valid data'
        );

        // Test filterGroupsWithData
        assertTrue(
            typeof ResultGrouper.filterGroupsWithData === 'function',
            moduleName,
            'filterGroupsWithData function exists'
        );
        const filteredEmpty = ResultGrouper.filterGroupsWithData({});
        assertDeepEqual(filteredEmpty, {}, moduleName, 'filterGroupsWithData - empty input');

        // Test groupsToArray
        assertTrue(
            typeof ResultGrouper.groupsToArray === 'function',
            moduleName,
            'groupsToArray function exists'
        );
        const arrayResult = ResultGrouper.groupsToArray({});
        assertTrue(Array.isArray(arrayResult), moduleName, 'groupsToArray - returns array');

        // Test getResultsFromGroups
        assertTrue(
            typeof ResultGrouper.getResultsFromGroups === 'function',
            moduleName,
            'getResultsFromGroups function exists'
        );
        const noResults = ResultGrouper.getResultsFromGroups({}, []);
        assertDeepEqual(noResults, [], moduleName, 'getResultsFromGroups - empty input');

        return { passed: 0, failed: 0, tests: [] };
    }

    /**************************************************************/
    /**
     * Runs all ChatConfig module tests.
     *
     * @returns {Object} Test results for this module
     */
    /**************************************************************/
    function runConfigTests() {
        const moduleName = 'ChatConfig';

        // Test API_CONFIG exists
        assertTrue(ChatConfig.API_CONFIG !== undefined, moduleName, 'API_CONFIG exists');

        // Test API_CONFIG is frozen
        // Note: In ES module strict mode, modifying a frozen object throws a TypeError
        assertThrows(() => {
            // Attempting to modify should throw in strict mode (Object.freeze + ES modules)
            ChatConfig.API_CONFIG.testProperty = 'test';
        }, moduleName, 'API_CONFIG is frozen (modification throws in strict mode)');
        assertEqual(
            ChatConfig.API_CONFIG.testProperty,
            undefined,
            moduleName,
            'API_CONFIG is frozen (modification has no effect)'
        );

        // Test endpoints exist
        assertTrue(ChatConfig.API_CONFIG.endpoints !== undefined, moduleName, 'API_CONFIG.endpoints exists');
        assertTrue(
            ChatConfig.API_CONFIG.endpoints.context !== undefined,
            moduleName,
            'endpoints.context exists'
        );
        assertTrue(
            ChatConfig.API_CONFIG.endpoints.interpret !== undefined,
            moduleName,
            'endpoints.interpret exists'
        );
        assertTrue(
            ChatConfig.API_CONFIG.endpoints.synthesize !== undefined,
            moduleName,
            'endpoints.synthesize exists'
        );

        // Test isLocalDevelopment function
        assertTrue(
            typeof ChatConfig.isLocalDevelopment === 'function',
            moduleName,
            'isLocalDevelopment function exists'
        );
        assertNoThrow(() => {
            ChatConfig.isLocalDevelopment();
        }, moduleName, 'isLocalDevelopment - executes without error');

        // Test buildUrl function
        assertTrue(typeof ChatConfig.buildUrl === 'function', moduleName, 'buildUrl function exists');
        const testUrl = ChatConfig.buildUrl('/api/test');
        assertTrue(testUrl.includes('/api/test'), moduleName, 'buildUrl - includes path');

        // Test getFetchOptions function
        assertTrue(typeof ChatConfig.getFetchOptions === 'function', moduleName, 'getFetchOptions function exists');
        const fetchOpts = ChatConfig.getFetchOptions();
        assertEqual(fetchOpts.credentials, 'include', moduleName, 'getFetchOptions - includes credentials');

        const fetchOptsWithExtras = ChatConfig.getFetchOptions({ method: 'POST' });
        assertEqual(fetchOptsWithExtras.method, 'POST', moduleName, 'getFetchOptions - merges extra options');
        assertEqual(
            fetchOptsWithExtras.credentials,
            'include',
            moduleName,
            'getFetchOptions - preserves credentials with extras'
        );

        return { passed: 0, failed: 0, tests: [] };
    }

    /**************************************************************/
    /**
     * Runs all tests for all modules.
     *
     * @returns {Promise<Object>} Complete test results with summary
     */
    /**************************************************************/
    async function runAllTests() {
        console.log('[TestRunner] Starting test run...');
        resetResults();

        // Load test data
        const data = await loadTestData();

        // Run all module tests with fault tolerance
        // Each module is wrapped in try-catch so one failure doesn't halt all tests
        const modules = [
            { name: 'ChatUtils', fn: () => runUtilsTests(data) },
            { name: 'MarkdownRenderer', fn: () => runMarkdownTests(data) },
            { name: 'ChatState', fn: () => runStateTests() },
            { name: 'ResultGrouper', fn: () => runResultGrouperTests(data) },
            { name: 'ChatConfig', fn: () => runConfigTests() }
        ];

        for (const module of modules) {
            try {
                module.fn();
            } catch (error) {
                console.error(`[TestRunner] Module ${module.name} threw an error:`, error);
                recordResult(
                    module.name,
                    'Module execution',
                    false,
                    `Module threw an unhandled error: ${error.message}`
                );
            }
        }

        console.log(`[TestRunner] Test run complete: ${testResults.passed}/${testResults.total} passed`);

        return {
            passed: testResults.passed,
            failed: testResults.failed,
            total: testResults.total,
            results: testResults.results,
            summary: formatSummary()
        };
    }

    /**************************************************************/
    /**
     * Formats test results as a readable summary string.
     *
     * @returns {string} Formatted summary
     */
    /**************************************************************/
    function formatSummary() {
        const lines = [];
        lines.push('## Test Results Summary\n');
        lines.push(`**Total:** ${testResults.total} tests`);
        lines.push(`**Passed:** ${testResults.passed} tests`);
        lines.push(`**Failed:** ${testResults.failed} tests`);
        lines.push(`**Pass Rate:** ${((testResults.passed / testResults.total) * 100).toFixed(1)}%\n`);

        // Group by module
        const byModule = {};
        testResults.results.forEach(r => {
            if (!byModule[r.module]) {
                byModule[r.module] = { passed: 0, failed: 0, tests: [] };
            }
            if (r.passed) {
                byModule[r.module].passed++;
            } else {
                byModule[r.module].failed++;
            }
            byModule[r.module].tests.push(r);
        });

        // Add module summaries
        lines.push('### Results by Module\n');
        for (const [module, data] of Object.entries(byModule)) {
            const status = data.failed === 0 ? 'PASS' : 'FAIL';
            lines.push(`**${module}:** ${data.passed}/${data.passed + data.failed} (${status})`);
        }

        // Add failed test details if any
        const failedTests = testResults.results.filter(r => !r.passed);
        if (failedTests.length > 0) {
            lines.push('\n### Failed Tests\n');
            failedTests.forEach(test => {
                lines.push(`- **${test.module}** - ${test.test}`);
                lines.push(`  - ${test.message}`);
                if (test.expected !== undefined) {
                    lines.push(`  - Expected: \`${test.expected}\``);
                }
                if (test.actual !== undefined) {
                    lines.push(`  - Actual: \`${test.actual}\``);
                }
            });
        }

        return lines.join('\n');
    }

    /**************************************************************/
    /**
     * Formats test results as markdown for chat display.
     *
     * @returns {string} Markdown formatted results
     */
    /**************************************************************/
    function formatResultsAsMarkdown() {
        return formatSummary();
    }

    /**************************************************************/
    /**
     * Public API for the test framework module.
     */
    /**************************************************************/
    return {
        // Main test runner
        runAllTests: runAllTests,

        // Individual module test runners
        runUtilsTests: runUtilsTests,
        runMarkdownTests: runMarkdownTests,
        runStateTests: runStateTests,
        runResultGrouperTests: runResultGrouperTests,
        runConfigTests: runConfigTests,

        // Test data management
        loadTestData: loadTestData,

        // Results formatting
        formatSummary: formatSummary,
        formatResultsAsMarkdown: formatResultsAsMarkdown,

        // Test results access
        getResults: () => ({ ...testResults })
    };
})();
