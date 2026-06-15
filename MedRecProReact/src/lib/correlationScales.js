/**************************************************************/
/**
 * Clamps a numeric value into a closed interval.
 *
 * @param {number} value - Candidate value.
 * @param {number} min - Minimum output.
 * @param {number} max - Maximum output.
 * @returns {number} Clamped value.
 */
function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

/**************************************************************/
/**
 * Converts a two-character hex pair into a number.
 *
 * @param {string} pair - Hex pair.
 * @returns {number} Channel value.
 */
function parseHexPair(pair) {
  return Number.parseInt(pair, 16);
}

/**************************************************************/
/**
 * Converts a CSS hex color into RGB channels.
 *
 * @param {string} hexColor - Six-digit hex color.
 * @returns {{ r: number, g: number, b: number }} RGB channels.
 */
function hexToRgb(hexColor) {
  const normalized = hexColor.replace('#', '');

  return {
    r: parseHexPair(normalized.slice(0, 2)),
    g: parseHexPair(normalized.slice(2, 4)),
    b: parseHexPair(normalized.slice(4, 6)),
  };
}

/**************************************************************/
/**
 * Converts one RGB channel into a two-character hex string.
 *
 * @param {number} value - RGB channel value.
 * @returns {string} Hex pair.
 */
function channelToHex(value) {
  return Math.round(clamp(value, 0, 255)).toString(16).padStart(2, '0');
}

/**************************************************************/
/**
 * Blends two colors by a ratio.
 *
 * @param {string} leftColor - Start color.
 * @param {string} rightColor - End color.
 * @param {number} ratio - Blend ratio from 0 to 1.
 * @returns {string} Hex color.
 */
function blendHex(leftColor, rightColor, ratio) {
  const boundedRatio = clamp(ratio, 0, 1);
  const left = hexToRgb(leftColor);
  const right = hexToRgb(rightColor);

  const r = left.r + ((right.r - left.r) * boundedRatio);
  const g = left.g + ((right.g - left.g) * boundedRatio);
  const b = left.b + ((right.b - left.b) * boundedRatio);

  return `#${channelToHex(r)}${channelToHex(g)}${channelToHex(b)}`;
}

export const CORRELATION_COLORS = {
  negative: '#3f9b7f',
  neutral: '#fef8e8',
  positive: '#e5771e',
};

/**************************************************************/
/**
 * Gets a diverging map color for a correlation coefficient.
 *
 * @param {number | null | undefined} coefficient - Coefficient from -1 to 1.
 * @returns {string | null} Hex color or null for suppressed cells.
 */
export function getCorrelationColor(coefficient) {
  if (coefficient === null || coefficient === undefined || !Number.isFinite(Number(coefficient))) {
    return null;
  }

  const boundedValue = clamp(Number(coefficient), -1, 1);

  if (boundedValue < 0) {
    return blendHex(CORRELATION_COLORS.neutral, CORRELATION_COLORS.negative, Math.abs(boundedValue));
  }

  return blendHex(CORRELATION_COLORS.neutral, CORRELATION_COLORS.positive, boundedValue);
}

/**************************************************************/
/**
 * Gets a diverging heatmap color for a LogRR value.
 *
 * @param {number | null | undefined} logRr - Natural-log relative risk.
 * @param {number} domain - Absolute LogRR value treated as full intensity.
 * @returns {string | null} Hex color or null for sparse cells.
 */
export function getLogRrColor(logRr, domain = 1.5) {
  if (logRr === null || logRr === undefined || !Number.isFinite(Number(logRr))) {
    return null;
  }

  const boundedDomain = Math.max(0.1, Number(domain));
  const scaledValue = clamp(Number(logRr) / boundedDomain, -1, 1);

  if (scaledValue < 0) {
    return blendHex(CORRELATION_COLORS.neutral, CORRELATION_COLORS.negative, Math.abs(scaledValue));
  }

  return blendHex(CORRELATION_COLORS.neutral, CORRELATION_COLORS.positive, scaledValue);
}

/**************************************************************/
/**
 * Gets an accessible foreground color for a diverging-cell background.
 *
 * @param {number | null | undefined} value - Signed scale value.
 * @returns {string} CSS color.
 */
export function getScaleTextColor(value) {
  if (value === null || value === undefined || !Number.isFinite(Number(value))) {
    return 'var(--color-text-tertiary)';
  }

  return Math.abs(Number(value)) > 0.62 ? '#ffffff' : 'var(--color-secondary)';
}
