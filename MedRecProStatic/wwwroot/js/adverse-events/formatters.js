/**************************************************************/
/**
 * Formatting helpers for AE dashboard display values.
 */
/**************************************************************/

/**************************************************************/
/**
 * Formats an integer-like count.
 *
 * @param {number|null|undefined} value Value to format.
 * @returns {string} Formatted count.
 */
/**************************************************************/
export function formatInteger(value) {
    const number = Number(value);
    return Number.isFinite(number) ? Math.round(number).toLocaleString() : '-';
}

/**************************************************************/
/**
 * Formats a relative-risk value.
 *
 * @param {number|null|undefined} value Value to format.
 * @returns {string} Formatted relative risk.
 */
/**************************************************************/
export function formatRR(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) {
        return '-';
    }

    return Math.abs(number) < 0.01 ? number.toExponential(1) : number.toFixed(2);
}

/**************************************************************/
/**
 * Formats a number-needed estimate.
 *
 * @param {number|null|undefined} value Number-needed value.
 * @returns {string} Formatted estimate.
 */
/**************************************************************/
export function formatNumberNeeded(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) {
        return '-';
    }

    if (number >= 9999) {
        return 'inf';
    }

    return Math.round(number).toLocaleString();
}

/**************************************************************/
/**
 * Formats a ratio as a percentage string.
 *
 * @param {number|null|undefined} value Fraction to format.
 * @param {number} digits Number of decimal places.
 * @returns {string} Percent display.
 */
/**************************************************************/
export function formatPercent(value, digits = 0) {
    const number = Number(value);
    return Number.isFinite(number) ? `${(number * 100).toFixed(digits)}%` : '-';
}

/**************************************************************/
/**
 * Formats an event count and denominator as a display string.
 *
 * @param {number|null|undefined} events Event count.
 * @param {number|null|undefined} denominator Denominator.
 * @returns {string} Event-rate display.
 */
/**************************************************************/
export function formatEventRate(events, denominator) {
    const eventNumber = Number(events);
    const denominatorNumber = Number(denominator);

    if (!Number.isFinite(eventNumber) || !Number.isFinite(denominatorNumber) || denominatorNumber <= 0) {
        return `${formatInteger(events)} / ${formatInteger(denominator)}`;
    }

    return `${formatInteger(eventNumber)} / ${formatInteger(denominatorNumber)} (${((eventNumber / denominatorNumber) * 100).toFixed(1)}%)`;
}

/**************************************************************/
/**
 * Gets the comparator query value expected by the API.
 *
 * @param {string} comparator Client comparator state.
 * @returns {string|null} API comparator value, or null for unfiltered.
 */
/**************************************************************/
export function comparatorToApiValue(comparator) {
    if (comparator === 'placebo') {
        return 'Placebo';
    }

    if (comparator === 'active') {
        return 'Active';
    }

    return null;
}

/**************************************************************/
/**
 * Formats a comparator display label.
 *
 * @param {string} comparator Comparator state.
 * @returns {string} Label text.
 */
/**************************************************************/
export function formatComparatorLabel(comparator) {
    if (comparator === 'placebo') {
        return 'Placebo';
    }

    if (comparator === 'active') {
        return 'Active comparator';
    }

    return 'All';
}

/**************************************************************/
/**
 * Converts a value to title-ish dashboard text.
 *
 * @param {string|null|undefined} value Text value.
 * @returns {string} Human display text.
 */
/**************************************************************/
export function sentenceLabel(value) {
    if (!value) {
        return '-';
    }

    return String(value)
        .replace(/([a-z])([A-Z])/g, '$1 $2')
        .replace(/[_-]+/g, ' ')
        .trim();
}
