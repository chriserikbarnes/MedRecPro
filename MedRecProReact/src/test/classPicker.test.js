import { describe, expect, it } from 'vitest';
import {
  buildClassSections,
  buildSystemSections,
  formatMapCellBadge,
  formatSystemMapCellBadge,
  getClassPickerChartableCount,
  getClassPickerDisplayCount,
  getSystemPickerChartableCount,
  getSystemPickerDisplayCount,
} from '../components/classPickerHelpers';

describe('ClassPicker helpers', () => {
  it('sections rows by map renderability with honest labels', () => {
    const sections = buildClassSections([
      { pharmClassCode: 'READY', hasRenderableMap: true },
      { pharmClassCode: 'NOMAP', hasRenderableMap: false },
    ]);

    expect(sections.map((section) => section.label)).toEqual([
      'Map-ready classes',
      'No map cells at current floor',
    ]);
    expect(sections[0].rows[0].pharmClassCode).toBe('READY');
    expect(sections[1].rows[0].pharmClassCode).toBe('NOMAP');
  });

  it('formats map-cell badges for ready and heatmap-only rows', () => {
    expect(formatMapCellBadge({ hasRenderableMap: true, usableMapCellCount: 1 })).toBe('1 map cell');
    expect(formatMapCellBadge({ hasRenderableMap: true, usableMapCellCount: 3 })).toBe('3 map cells');
    expect(formatMapCellBadge({ hasRenderableMap: false, maxPairCount: 2 })).toBe('max 2 pairs');
    expect(formatMapCellBadge({ hasRenderableMap: false, maxPairCount: 0 })).toBe('no map cells');
  });

  it('prefers total matching count over loaded page count', () => {
    expect(getClassPickerDisplayCount(87, 50)).toBe(87);
    expect(getClassPickerDisplayCount(0, 50)).toBe(50);
  });

  it('prefers total chartable count over loaded map-ready rows', () => {
    const classes = [
      { pharmClassCode: 'READY', hasRenderableMap: true },
      { pharmClassCode: 'NOMAP', hasRenderableMap: false },
    ];

    expect(getClassPickerChartableCount(42, classes)).toBe(42);
    expect(getClassPickerChartableCount(0, classes)).toBe(1);
  });

  it('sections system rows by map renderability with system labels', () => {
    const sections = buildSystemSections([
      { systemOrganClass: 'Cardiac Disorders', hasRenderableMap: true },
      { systemOrganClass: 'Vascular Disorders', hasRenderableMap: false },
    ]);

    expect(sections.map((section) => section.label)).toEqual([
      'Map-ready systems',
      'No map cells at current floor',
    ]);
    expect(sections[0].rows[0].systemOrganClass).toBe('Cardiac Disorders');
    expect(sections[1].rows[0].systemOrganClass).toBe('Vascular Disorders');
  });

  it('formats system map-cell badges for ready and heatmap-only rows', () => {
    expect(formatSystemMapCellBadge({ hasRenderableMap: true, usableMapCellCount: 1 })).toBe('1 map cell');
    expect(formatSystemMapCellBadge({ hasRenderableMap: true, usableMapCellCount: 3 })).toBe('3 map cells');
    expect(formatSystemMapCellBadge({ hasRenderableMap: false, maxPairCount: 2 })).toBe('max 2 terms');
    expect(formatSystemMapCellBadge({ hasRenderableMap: false, maxPairCount: 0 })).toBe('no map cells');
  });

  it('chooses system picker counts from headers or loaded rows', () => {
    const systems = [
      { systemOrganClass: 'Cardiac Disorders', hasRenderableMap: true },
      { systemOrganClass: 'Vascular Disorders', hasRenderableMap: false },
    ];

    expect(getSystemPickerDisplayCount(17, 2)).toBe(17);
    expect(getSystemPickerDisplayCount(0, 2)).toBe(2);
    expect(getSystemPickerChartableCount(9, systems)).toBe(9);
    expect(getSystemPickerChartableCount(0, systems)).toBe(1);
  });
});
