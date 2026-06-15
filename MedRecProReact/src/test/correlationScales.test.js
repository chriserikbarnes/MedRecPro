import { describe, expect, it } from 'vitest';
import {
  CORRELATION_COLORS,
  getCorrelationColor,
  getLogRrColor,
  getScaleTextColor,
} from '../lib/correlationScales';

describe('correlationScales', () => {
  it('centers the correlation scale at the neutral color', () => {
    expect(getCorrelationColor(0)).toBe(CORRELATION_COLORS.neutral);
    expect(getCorrelationColor(null)).toBeNull();
  });

  it('uses different diverging colors for negative and positive coefficients', () => {
    expect(getCorrelationColor(-1)).toBe(CORRELATION_COLORS.negative);
    expect(getCorrelationColor(1)).toBe(CORRELATION_COLORS.positive);
    expect(getCorrelationColor(-0.5)).not.toBe(getCorrelationColor(0.5));
  });

  it('maps LogRR values onto the same semantic directions', () => {
    expect(getLogRrColor(-1.5)).toBe(CORRELATION_COLORS.negative);
    expect(getLogRrColor(0)).toBe(CORRELATION_COLORS.neutral);
    expect(getLogRrColor(1.5)).toBe(CORRELATION_COLORS.positive);
  });

  it('chooses high-contrast text for strong scale values', () => {
    expect(getScaleTextColor(0.8)).toBe('#ffffff');
    expect(getScaleTextColor(0.2)).toBe('var(--color-secondary)');
    expect(getScaleTextColor(null)).toBe('var(--color-text-tertiary)');
  });
});
