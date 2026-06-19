import { describe, expect, it } from 'vitest';
import { scalePoint, scaleReferenceLine } from '../components/correlation/correlationScatter';

describe('correlation scatter helpers', () => {
  it('allows point scaling to extrapolate while clamping reference lines inside the plot', () => {
    const positiveDomain = { min: 0.5, max: 1.5 };

    expect(scalePoint(0, positiveDomain, 184, 28)).toBeGreaterThan(184);
    expect(scaleReferenceLine(0, positiveDomain, 184, 28)).toBe(184);
  });

  it('clamps reference lines across reversed and forward plot ranges', () => {
    const negativeDomain = { min: -1.5, max: -0.5 };

    expect(scaleReferenceLine(0, negativeDomain, 184, 28)).toBe(28);
    expect(scaleReferenceLine(0, negativeDomain, 36, 292)).toBe(292);
  });
});
