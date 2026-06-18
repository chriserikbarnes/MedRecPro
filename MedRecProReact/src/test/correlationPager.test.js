import { describe, expect, it } from 'vitest';
import {
  formatPageLabel,
  isPageDirectionDisabled,
  normalizePageSize,
  resetPageOnDependencyChange,
} from '../components/correlation/correlationPagerHelpers';

describe('CorrelationPager helpers', () => {
  it('formats page labels with item counts', () => {
    expect(formatPageLabel({
      pageNumber: 2,
      totalPages: 5,
      totalCount: 83,
    }, 'classes')).toBe('Page 2 of 5 - 83 classes');
  });

  it('normalizes page sizes to supported options', () => {
    expect(normalizePageSize(40, [20, 40, 80])).toBe(40);
    expect(normalizePageSize(45, [20, 40, 80])).toBe(40);
    expect(normalizePageSize(72, [20, 40, 80])).toBe(80);
  });

  it('detects disabled previous and next directions', () => {
    const page = {
      hasPreviousPage: false,
      hasNextPage: true,
    };

    expect(isPageDirectionDisabled(page, 'previous')).toBe(true);
    expect(isPageDirectionDisabled(page, 'next')).toBe(false);
    expect(isPageDirectionDisabled(null, 'next')).toBe(true);
  });

  it('resets the page when a dependency changes', () => {
    expect(resetPageOnDependencyChange(4, 'a', 'a')).toBe(4);
    expect(resetPageOnDependencyChange(4, 'a', 'b')).toBe(1);
  });
});
