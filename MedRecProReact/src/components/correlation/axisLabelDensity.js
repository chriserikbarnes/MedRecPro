/**************************************************************/
/**
 * Gets a deterministic visual-label sampling step for dense chart axes.
 *
 * @param {number} axisCount - Number of axis entries in the visible grid.
 * @returns {number} Render every nth label.
 */
export function getAxisLabelStep(axisCount) {
  const count = Number(axisCount) || 0;

  if (count > 140) {
    return 16;
  }

  if (count > 96) {
    return 12;
  }

  if (count > 72) {
    return 8;
  }

  if (count > 48) {
    return 4;
  }

  if (count > 32) {
    return 2;
  }

  return 1;
}

/**************************************************************/
/**
 * Determines whether one axis label should render in the visual layer.
 *
 * @param {number} index - Zero-based axis index.
 * @param {number} axisCount - Number of axis entries in the visible grid.
 * @returns {boolean} True when the label should be visible.
 */
export function shouldRenderAxisLabel(index, axisCount) {
  const count = Number(axisCount) || 0;
  const step = getAxisLabelStep(count);

  if (index === 0 || index === count - 1) {
    return true;
  }

  if (count > 72 && count - 1 - index < step) {
    return false;
  }

  return index % step === 0;
}

/**************************************************************/
/**
 * Builds density-state class names for correlation grids.
 *
 * @param {number} axisCount - Number of axis entries in the visible grid.
 * @param {{ hideCellValues?: boolean }} options - Density behavior options.
 * @returns {string} Space-delimited class names.
 */
export function getAxisDensityClassName(axisCount, { hideCellValues = false } = {}) {
  const count = Number(axisCount) || 0;
  const classNames = [];

  if (count > 32) {
    classNames.push('dense-axis');
  }

  if (count > 96) {
    classNames.push('super-dense-axis');
  }

  if (hideCellValues && count > 36) {
    classNames.push('hide-cell-values');
  }

  return classNames.join(' ');
}
