export const DEFAULT_FOREST_DOMAIN = Object.freeze({ min: 0.1, max: 10 });
export const DEFAULT_FOREST_TICKS = Object.freeze([0.1, 0.25, 0.5, 1, 2, 4, 10]);
export const FOREST_EDGE_INSET_PERCENT = 2.5;

const FRIENDLY_MANTISSAS = [1, 2, 5];

// Tick caps thin the axis labels by viewport so they never overlap on phones.
export const MAX_FOREST_TICKS = 11;
export const COMPACT_FOREST_TICKS = 6;
export const COMPACT_FOREST_TICKS_NARROW = 4;

const FLOAT_TOLERANCE = 1e-12;

/**************************************************************/
/**
 * Converts a candidate value into a positive finite number for log plotting.
 *
 * @param {number | string | null | undefined} value - Candidate ratio value.
 * @returns {number | null} Positive finite number or null.
 */
function toPositiveFiniteNumber(value) {
  // Nullish and non-positive values cannot be rendered on a log axis.
  if (!Number.isFinite(Number(value)) || Number(value) <= 0) {
    return null;
  }

  return Number(value);
}

/**************************************************************/
/**
 * Compares two numeric values using a small relative tolerance.
 *
 * @param {number} left - First value.
 * @param {number} right - Second value.
 * @returns {boolean} Whether the values are effectively equal.
 */
function nearlyEqual(left, right) {
  // Relative tolerance keeps decimal tick comparisons stable across powers.
  return Math.abs(left - right) <= Math.max(Math.abs(left), Math.abs(right), 1) * FLOAT_TOLERANCE;
}

/**************************************************************/
/**
 * Rounds tick values enough to remove binary floating-point noise.
 *
 * @param {number} value - Tick value.
 * @returns {number} Rounded tick value.
 */
function normalizeTickValue(value) {
  return Number(value.toPrecision(12));
}

/**************************************************************/
/**
 * Finds the next friendly log tick below or above a plotted value.
 *
 * @param {number} value - Positive finite ratio value.
 * @param {'below' | 'above'} direction - Search direction.
 * @returns {number} Friendly log tick.
 */
function getFriendlyTickOutside(value, direction) {
  const exponent = Math.floor(Math.log10(value));
  const candidates = [];

  // Adjacent powers are enough because 1/2/5 ticks repeat every decade.
  for (let power = exponent - 1; power <= exponent + 1; power += 1) {
    for (const mantissa of FRIENDLY_MANTISSAS) {
      candidates.push(normalizeTickValue(mantissa * (10 ** power)));
    }
  }

  if (direction === 'below') {
    const lowerTicks = candidates.filter((tick) => tick < value && !nearlyEqual(tick, value));

    return lowerTicks.length > 0 ? Math.max(...lowerTicks) : normalizeTickValue(value / 10);
  }

  const upperTicks = candidates.filter((tick) => tick > value && !nearlyEqual(tick, value));

  return upperTicks.length > 0 ? Math.min(...upperTicks) : normalizeTickValue(value * 10);
}

/**************************************************************/
/**
 * Normalizes an optional domain into a positive ascending log domain.
 *
 * @param {{ min?: number, max?: number } | null | undefined} domain - Candidate domain.
 * @returns {{ min: number, max: number }} Positive ascending domain.
 */
function normalizeDomain(domain) {
  const min = toPositiveFiniteNumber(domain?.min);
  const max = toPositiveFiniteNumber(domain?.max);

  // Invalid domains fall back to the prototype's primary forest scale.
  if (min === null || max === null || min >= max) {
    return DEFAULT_FOREST_DOMAIN;
  }

  return { min, max };
}

/**************************************************************/
/**
 * Reduces expanded-domain ticks so labels remain readable.
 *
 * @param {number[]} ticks - Candidate tick values.
 * @param {number} [maxTicks] - Maximum labels to retain; defaults to the desktop cap.
 * @returns {number[]} Sparse tick list.
 */
function sparsifyTicks(ticks, maxTicks = MAX_FOREST_TICKS) {
  // Prototype/default domains are already compact enough.
  if (ticks.length <= maxTicks) {
    return ticks;
  }

  let step = 2;
  let selectedTicks = ticks;

  // Keep the domain endpoints and RR=1 while thinning intermediate labels.
  while (selectedTicks.length > maxTicks) {
    selectedTicks = ticks.filter((tick, index) => (
      index === 0
      || index === ticks.length - 1
      || nearlyEqual(tick, 1)
      || index % step === 0
    ));
    step += 1;
  }

  return selectedTicks;
}

/**************************************************************/
/**
 * Builds a safe forest-plot log domain from the rendered signal values.
 *
 * @param {object[]} signals - Rendered forest signal rows.
 * @returns {{ min: number, max: number }} Positive log domain.
 */
export function getForestScaleDomain(signals) {
  const values = [];

  // RR, lower CI, and upper CI all participate in the visible plot domain.
  for (const signal of Array.isArray(signals) ? signals : []) {
    for (const value of [signal?.rr, signal?.rrL, signal?.rrH]) {
      const numericValue = toPositiveFiniteNumber(value);

      if (numericValue !== null) {
        values.push(numericValue);
      }
    }
  }

  // Empty or unplottable payloads keep the prototype domain.
  if (values.length === 0) {
    return DEFAULT_FOREST_DOMAIN;
  }

  const dataMin = Math.min(...values);
  const dataMax = Math.max(...values);

  // Expand only when the data would otherwise exceed the prototype domain.
  const min = dataMin < DEFAULT_FOREST_DOMAIN.min
    ? getFriendlyTickOutside(dataMin, 'below')
    : DEFAULT_FOREST_DOMAIN.min;

  const max = dataMax > DEFAULT_FOREST_DOMAIN.max
    ? getFriendlyTickOutside(dataMax, 'above')
    : DEFAULT_FOREST_DOMAIN.max;

  return { min, max };
}

/**************************************************************/
/**
 * Gets readable log-axis ticks for a forest-plot domain.
 *
 * @param {{ min?: number, max?: number } | null | undefined} domain - Forest scale domain.
 * @param {number} [maxTicks] - Maximum labels to retain; lower it on narrow viewports.
 * @returns {number[]} Tick values.
 */
export function getForestTicks(domain, maxTicks = MAX_FOREST_TICKS) {
  const normalizedDomain = normalizeDomain(domain);

  // Preserve the exact prototype tick set when no expansion is needed.
  if (
    nearlyEqual(normalizedDomain.min, DEFAULT_FOREST_DOMAIN.min)
    && nearlyEqual(normalizedDomain.max, DEFAULT_FOREST_DOMAIN.max)
  ) {
    return DEFAULT_FOREST_TICKS;
  }

  const ticks = [];
  const startPower = Math.floor(Math.log10(normalizedDomain.min)) - 1;
  const endPower = Math.ceil(Math.log10(normalizedDomain.max)) + 1;

  // Expanded domains use the conventional 1/2/5 cadence across decades.
  for (let power = startPower; power <= endPower; power += 1) {
    for (const mantissa of FRIENDLY_MANTISSAS) {
      const tick = normalizeTickValue(mantissa * (10 ** power));

      if (
        tick >= normalizedDomain.min
        && tick <= normalizedDomain.max
        && !ticks.some((existingTick) => nearlyEqual(existingTick, tick))
      ) {
        ticks.push(tick);
      }
    }
  }

  // RR=1 should remain visible because it carries the interpretation boundary.
  if (!ticks.some((tick) => nearlyEqual(tick, 1))) {
    ticks.push(1);
  }

  return sparsifyTicks(ticks.sort((left, right) => left - right), maxTicks);
}

/**************************************************************/
/**
 * Calculates an inset log-scale x-axis percentage for a forest-plot value.
 *
 * @param {number | null | undefined} value - RR or CI value.
 * @param {{ min?: number, max?: number } | null | undefined} domain - Forest scale domain.
 * @returns {number | null} Percentage from zero to one hundred.
 */
export function getForestXPercent(value, domain = DEFAULT_FOREST_DOMAIN) {
  const numericValue = toPositiveFiniteNumber(value);

  // Missing or non-positive values cannot be plotted on a log axis.
  if (numericValue === null) {
    return null;
  }

  const normalizedDomain = normalizeDomain(domain);
  const logMin = Math.log10(normalizedDomain.min);
  const logMax = Math.log10(normalizedDomain.max);
  const logValue = Math.log10(numericValue);
  const rawPercent = ((logValue - logMin) / (logMax - logMin)) * 100;
  const clampedPercent = Math.min(Math.max(rawPercent, 0), 100);
  const drawableRange = 100 - (FOREST_EDGE_INSET_PERCENT * 2);

  return FOREST_EDGE_INSET_PERCENT + ((clampedPercent / 100) * drawableRange);
}

/**************************************************************/
/**
 * Formats a forest tick for compact axis display.
 *
 * @param {number | null | undefined} value - Tick value.
 * @returns {string} Display label.
 */
export function formatForestTick(value) {
  const numericValue = toPositiveFiniteNumber(value);

  if (numericValue === null) {
    return '';
  }

  if (numericValue >= 10) {
    return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(numericValue);
  }

  if (numericValue >= 1) {
    return new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 }).format(numericValue);
  }

  if (numericValue >= 0.01) {
    return new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(numericValue);
  }

  if (numericValue >= 0.001) {
    return new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 }).format(numericValue);
  }

  return numericValue.toExponential(0);
}
