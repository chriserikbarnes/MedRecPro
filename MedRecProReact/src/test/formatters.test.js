import { describe, expect, it } from 'vitest';
import {
  formatComparatorCoverage,
  formatCountLabel,
  formatDecimal,
  formatDenominators,
  formatDose,
  formatFavoriteAction,
  formatInteger,
  formatPercent,
} from '../lib/formatters';

describe('formatters', () => {
  it('formats integer and decimal values with stable fallbacks', () => {
    expect(formatInteger(1234.4)).toBe('1,234');
    expect(formatInteger(null)).toBe('0');
    expect(formatDecimal(1.234, 2)).toBe('1.23');
    expect(formatDecimal(undefined, 2)).toBe('-');
  });

  it('formats dose, percent, and denominators without implying missing values', () => {
    expect(formatDose(21.5, 'mcg')).toBe('21.5 mcg');
    expect(formatDose(null, 'mcg')).toBe('');
    expect(formatPercent(0.455)).toBe('46%');
    expect(formatDenominators(null, null)).toBe('-');
    expect(formatDenominators(1200, 1133)).toBe('1,200 / 1,133');
  });

  it('formats singular and plural count labels', () => {
    expect(formatCountLabel(1, 'drug')).toBe('1 drug');
    expect(formatCountLabel(2, 'drug')).toBe('2 drugs');
    expect(formatCountLabel(null, 'drug')).toBe('0 drugs');
    expect(formatCountLabel(1, 'class', 'classes')).toBe('1 class');
    expect(formatCountLabel(2, 'class', 'classes')).toBe('2 classes');
  });

  it('formats comparator coverage and favorite action labels', () => {
    expect(formatComparatorCoverage({ placeboCoverage: true, activeCoverage: true })).toBe('Placebo and active');
    expect(formatComparatorCoverage({ placeboCoverage: true, activeCoverage: false })).toBe('Placebo');
    expect(formatComparatorCoverage({ placeboCoverage: false, activeCoverage: true })).toBe('Active comparator');
    expect(formatComparatorCoverage({ placeboCoverage: false, activeCoverage: false })).toBe('Comparator limited');
    expect(formatFavoriteAction(true)).toBe('Favorited');
    expect(formatFavoriteAction(false)).toBe('Favorite');
  });
});
