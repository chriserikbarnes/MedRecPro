/**************************************************************/
/**
 * Finds the finite domain for a scatter axis.
 *
 * @param {number[]} values - Numeric values.
 * @returns {{ min: number, max: number }} Axis domain.
 */
export function getScatterDomain(values) {
  const finiteValues = values.filter((value) => Number.isFinite(value));

  if (finiteValues.length === 0) {
    return { min: -1, max: 1 };
  }

  const min = Math.min(...finiteValues);
  const max = Math.max(...finiteValues);
  const spread = Math.max(0.5, max - min);
  const padding = spread * 0.15;

  return {
    min: min - padding,
    max: max + padding,
  };
}

/**************************************************************/
/**
 * Converts a domain value into an SVG coordinate.
 *
 * @param {number} value - Data value.
 * @param {{ min: number, max: number }} domain - Axis domain.
 * @param {number} low - Low output coordinate.
 * @param {number} high - High output coordinate.
 * @returns {number} Coordinate.
 */
export function scalePoint(value, domain, low, high) {
  if (!Number.isFinite(value) || domain.max === domain.min) {
    return (low + high) / 2;
  }

  const ratio = (value - domain.min) / (domain.max - domain.min);

  return low + (ratio * (high - low));
}

/**************************************************************/
/**
 * Converts a reference-line value into a coordinate that stays inside the plot range.
 *
 * @param {number} value - Reference value.
 * @param {{ min: number, max: number }} domain - Axis domain.
 * @param {number} low - Low output coordinate.
 * @param {number} high - High output coordinate.
 * @returns {number} Coordinate clamped to the SVG plot range.
 */
export function scaleReferenceLine(value, domain, low, high) {
  const coordinate = scalePoint(value, domain, low, high);
  const minCoordinate = Math.min(low, high);
  const maxCoordinate = Math.max(low, high);

  return Math.min(maxCoordinate, Math.max(minCoordinate, coordinate));
}
