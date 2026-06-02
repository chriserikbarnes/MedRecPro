/**************************************************************/
/**
 * Formats an integer count with a stable fallback.
 *
 * @param {number | null | undefined} value - Count to format.
 * @returns {string} Formatted count.
 */
export function formatInteger(value) {
  // Nullish values mean the API did not provide the metric.
  if (value === null || value === undefined || !Number.isFinite(Number(value))) {
    return '0';
  }

  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(Number(value));
}

/**************************************************************/
/**
 * Formats a nullable ratio or score with compact precision.
 *
 * @param {number | null | undefined} value - Ratio to format.
 * @param {number} digits - Maximum fraction digits.
 * @returns {string} Formatted ratio.
 */
export function formatDecimal(value, digits = 1) {
  // Missing values render as a dash to avoid implying zero.
  if (value === null || value === undefined || !Number.isFinite(Number(value))) {
    return '-';
  }

  return new Intl.NumberFormat('en-US', {
    maximumFractionDigits: digits,
  }).format(Number(value));
}

/**************************************************************/
/**
 * Formats a dose value with its unit for an AE row chip.
 *
 * @param {number | null | undefined} dose - Numeric dose value.
 * @param {string | null | undefined} unit - Dose unit (e.g. "mcg").
 * @returns {string} Formatted dose (e.g. "21 mcg"), or '' when no dose is present.
 */
export function formatDose(dose, unit) {
  // Empty string lets the caller skip rendering the chip when no dose exists.
  if (dose === null || dose === undefined || !Number.isFinite(Number(dose))) {
    return '';
  }

  // Intl drops trailing zeros so 21.0 renders as "21" while 0.5 stays "0.5".
  const formatted = new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 }).format(Number(dose));

  // Append the unit only when the API supplies one.
  const trimmedUnit = typeof unit === 'string' ? unit.trim() : '';
  return trimmedUnit ? `${formatted} ${trimmedUnit}` : formatted;
}

/**************************************************************/
/**
 * Formats a fraction as a percentage.
 *
 * @param {number | null | undefined} value - Fraction between zero and one.
 * @returns {string} Formatted percent.
 */
export function formatPercent(value) {
  // Missing coverage values render as zero because summary DTOs default them to zero.
  if (value === null || value === undefined || !Number.isFinite(Number(value))) {
    return '0%';
  }

  return new Intl.NumberFormat('en-US', {
    style: 'percent',
    maximumFractionDigits: 0,
  }).format(Number(value));
}

/**************************************************************/
/**
 * Formats denominator pairs for KPI labels.
 *
 * @param {number | null | undefined} treatmentN - Treatment-arm denominator.
 * @param {number | null | undefined} comparatorN - Comparator-arm denominator.
 * @returns {string} Formatted denominator pair.
 */
export function formatDenominators(treatmentN, comparatorN) {
  // Use a dash when both denominators are absent.
  if (treatmentN === null && comparatorN === null) {
    return '-';
  }

  return `${formatDecimal(treatmentN, 0)} / ${formatDecimal(comparatorN, 0)}`;
}

/**************************************************************/
/**
 * Builds a compact comparator coverage phrase.
 *
 * @param {object} product - Product view model.
 * @returns {string} Coverage phrase.
 */
export function formatComparatorCoverage(product) {
  // The flags are independent, so both may be true.
  if (product.placeboCoverage && product.activeCoverage) {
    return 'Placebo and active';
  }

  // Placebo-only products are useful for causality review.
  if (product.placeboCoverage) {
    return 'Placebo';
  }

  // Active-comparator products are useful for interchange.
  if (product.activeCoverage) {
    return 'Active comparator';
  }

  return 'Comparator limited';
}

/**************************************************************/
/**
 * Converts favorite API status into the short product row label.
 *
 * @param {boolean} isFavorite - Favorite flag.
 * @returns {string} Button label.
 */
export function formatFavoriteAction(isFavorite) {
  // Favorited rows use a removal action; others use an add action.
  return isFavorite ? 'Favorited' : 'Favorite';
}
