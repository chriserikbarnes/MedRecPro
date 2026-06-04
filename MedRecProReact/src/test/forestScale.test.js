import { describe, expect, it } from 'vitest';
import {
  DEFAULT_FOREST_DOMAIN,
  DEFAULT_FOREST_TICKS,
  formatForestTick,
  getForestScaleDomain,
  getForestTicks,
  getForestXPercent,
} from '../lib/forestScale';

describe('forestScale', () => {
  it('returns the prototype domain and ticks for empty or in-range data', () => {
    expect(getForestScaleDomain([])).toEqual(DEFAULT_FOREST_DOMAIN);
    expect(getForestTicks(DEFAULT_FOREST_DOMAIN)).toEqual(DEFAULT_FOREST_TICKS);
    expect(getForestScaleDomain([{ rr: 2, rrL: 0.5, rrH: 4 }])).toEqual(DEFAULT_FOREST_DOMAIN);
  });

  it('expands the log domain with friendly tick values when data exceed defaults', () => {
    const domain = getForestScaleDomain([
      { rr: 0.03, rrL: 0.02, rrH: 25 },
    ]);

    expect(domain.min).toBeLessThanOrEqual(0.01);
    expect(domain.max).toBeGreaterThanOrEqual(50);
    expect(getForestTicks(domain)).toContain(1);
  });

  it('positions RR=1 inside the drawable track and rejects invalid values', () => {
    const midpoint = getForestXPercent(1, DEFAULT_FOREST_DOMAIN);

    expect(midpoint).toBeGreaterThan(45);
    expect(midpoint).toBeLessThan(55);
    expect(getForestXPercent(0, DEFAULT_FOREST_DOMAIN)).toBeNull();
    expect(getForestXPercent(null, DEFAULT_FOREST_DOMAIN)).toBeNull();
  });

  it('formats compact forest tick labels', () => {
    expect(formatForestTick(10)).toBe('10');
    expect(formatForestTick(2.5)).toBe('2.5');
    expect(formatForestTick(0.25)).toBe('0.25');
    expect(formatForestTick(null)).toBe('');
  });
});
