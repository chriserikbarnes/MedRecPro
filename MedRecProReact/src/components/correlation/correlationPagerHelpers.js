import { formatInteger } from '../../lib/formatters';

export const DEFAULT_PAGE_SIZE_OPTIONS = [20, 40, 80, 100];

/**************************************************************/
/**
 * Clamps a requested page size to the closest supported option.
 *
 * @param {unknown} value - Candidate page size.
 * @param {number[]} options - Supported page sizes.
 * @returns {number} Normalized page size.
 */
export function normalizePageSize(value, options = DEFAULT_PAGE_SIZE_OPTIONS) {
  const numericValue = Number(value);
  const validOptions = options.filter((option) => Number.isFinite(option) && option > 0);

  if (validOptions.length === 0) {
    return 1;
  }

  if (validOptions.includes(numericValue)) {
    return numericValue;
  }

  return validOptions.reduce((closest, option) =>
    Math.abs(option - numericValue) < Math.abs(closest - numericValue) ? option : closest,
  validOptions[0]);
}

/**************************************************************/
/**
 * Formats compact page metadata for correlation controls.
 *
 * @param {object | null | undefined} page - Axis page metadata.
 * @param {string} itemLabel - Display label for the paged item type.
 * @returns {string} Page label.
 */
export function formatPageLabel(page, itemLabel = 'items') {
  const pageNumber = page?.pageNumber ?? 1;
  const totalPages = Math.max(1, page?.totalPages ?? 1);
  const totalCount = page?.totalCount ?? 0;

  return `Page ${formatInteger(pageNumber)} of ${formatInteger(totalPages)} - ${formatInteger(totalCount)} ${itemLabel}`;
}

/**************************************************************/
/**
 * Reports whether a pager button should be disabled.
 *
 * @param {object | null | undefined} page - Axis page metadata.
 * @param {'previous' | 'next'} direction - Requested direction.
 * @returns {boolean} True when the direction cannot be paged.
 */
export function isPageDirectionDisabled(page, direction) {
  if (!page) {
    return true;
  }

  return direction === 'previous' ? !page.hasPreviousPage : !page.hasNextPage;
}

/**************************************************************/
/**
 * Returns the first page when a dependency value changes.
 *
 * @param {number} currentPageNumber - Current 1-based page number.
 * @param {unknown} previousDependency - Previous dependency snapshot.
 * @param {unknown} nextDependency - Next dependency snapshot.
 * @returns {number} Current page or one when dependencies differ.
 */
export function resetPageOnDependencyChange(currentPageNumber, previousDependency, nextDependency) {
  return Object.is(previousDependency, nextDependency) ? currentPageNumber : 1;
}
