import { describe, expect, it } from 'vitest';
import {
  getAxisDensityClassName,
  getAxisLabelStep,
  shouldRenderAxisLabel,
} from '../components/correlation/axisLabelDensity';

describe('axisLabelDensity helpers', () => {
  it('increases label sampling as axis density rises', () => {
    expect(getAxisLabelStep(12)).toBe(1);
    expect(getAxisLabelStep(25)).toBe(1);
    expect(getAxisLabelStep(32)).toBe(1);
    expect(getAxisLabelStep(40)).toBe(2);
    expect(getAxisLabelStep(60)).toBe(4);
    expect(getAxisLabelStep(80)).toBe(8);
    expect(getAxisLabelStep(120)).toBe(12);
    expect(getAxisLabelStep(160)).toBe(16);
  });

  it('renders every x-axis label for small matrices', () => {
    expect(
      Array.from({ length: 25 }, (_, index) => shouldRenderAxisLabel(index, 25)).every(Boolean),
    ).toBe(true);
  });

  it('keeps edge labels visible while sampling interior labels', () => {
    expect(shouldRenderAxisLabel(0, 40)).toBe(true);
    expect(shouldRenderAxisLabel(1, 40)).toBe(false);
    expect(shouldRenderAxisLabel(2, 40)).toBe(true);
    expect(shouldRenderAxisLabel(39, 40)).toBe(true);
  });

  it('avoids near-edge label pileups on super-dense axes', () => {
    expect(shouldRenderAxisLabel(0, 100)).toBe(true);
    expect(shouldRenderAxisLabel(84, 100)).toBe(true);
    expect(shouldRenderAxisLabel(96, 100)).toBe(false);
    expect(shouldRenderAxisLabel(99, 100)).toBe(true);
  });

  it('marks dense grids and optionally hides cell values', () => {
    expect(getAxisDensityClassName(32)).toBe('');
    expect(getAxisDensityClassName(33)).toBe('dense-axis');
    expect(getAxisDensityClassName(97)).toBe('dense-axis super-dense-axis');
    expect(getAxisDensityClassName(100, { hideCellValues: true })).toBe(
      'dense-axis super-dense-axis hide-cell-values',
    );
  });
});
