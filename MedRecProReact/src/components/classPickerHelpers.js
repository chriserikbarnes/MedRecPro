import { formatInteger } from '../lib/formatters';

/**************************************************************/
/**
 * Builds picker sections for pharmacologic classes.
 *
 * @param {object[]} classes - Class rows.
 * @returns {object[]} Picker sections.
 */
export function buildClassSections(classes) {
  const mapReadyRows = classes.filter((item) => item.hasRenderableMap);
  const noMapRows = classes.filter((item) => !item.hasRenderableMap);
  const sections = [];

  if (mapReadyRows.length > 0) {
    sections.push({ id: 'map-ready', label: 'Map-ready classes', rows: mapReadyRows });
  }

  if (noMapRows.length > 0) {
    sections.push({ id: 'no-map-cells', label: 'No map cells at current floor', rows: noMapRows });
  }

  return sections;
}

/**************************************************************/
/**
 * Formats the renderability badge for a class picker row.
 *
 * @param {object} item - Class row.
 * @returns {string} Badge text.
 */
export function formatMapCellBadge(item) {
  if (item.hasRenderableMap) {
    const cellCount = item.usableMapCellCount ?? 0;
    return `${formatInteger(cellCount)} map ${cellCount === 1 ? 'cell' : 'cells'}`;
  }

  if ((item.maxPairCount ?? 0) > 0) {
    return `max ${formatInteger(item.maxPairCount)} pairs`;
  }

  return 'no map cells';
}

/**************************************************************/
/**
 * Chooses the count shown in the class picker search row.
 *
 * @param {number} totalClassCount - Total matching class count from the API.
 * @param {number} loadedClassCount - Locally loaded row count.
 * @returns {number} Display count.
 */
export function getClassPickerDisplayCount(totalClassCount, loadedClassCount) {
  return Number.isFinite(totalClassCount) && totalClassCount > 0
    ? totalClassCount
    : loadedClassCount;
}

/**************************************************************/
/**
 * Chooses the chartable count shown in the class picker search row.
 *
 * @param {number} chartableClassCount - Total chartable class count from the API.
 * @param {object[]} classes - Locally loaded class rows.
 * @returns {number} Display count.
 */
export function getClassPickerChartableCount(chartableClassCount, classes) {
  return Number.isFinite(chartableClassCount) && chartableClassCount > 0
    ? chartableClassCount
    : classes.filter((item) => item.hasRenderableMap).length;
}
