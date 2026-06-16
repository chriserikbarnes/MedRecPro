import { describe, expect, it } from 'vitest';
import { getMapCell } from '../components/correlation/correlationMapCells';

describe('CorrelationMap helpers', () => {
  it('synthesizes the upper-left diagonal cell for sparse map payloads', () => {
    const rowSoc = { name: 'Blood & Lymphatic', index: 0 };
    const columnSoc = { name: 'Blood & Lymphatic', index: 0 };

    const cell = getMapCell(new Map(), rowSoc, columnSoc);

    expect(cell).toMatchObject({
      rowIndex: 0,
      columnIndex: 0,
      rowSoc: 'Blood & Lymphatic',
      columnSoc: 'Blood & Lymphatic',
      coefficient: 1,
      pairCount: 0,
      isDiagonal: true,
    });
  });

  it('leaves missing off-diagonal cells blank', () => {
    const rowSoc = { name: 'Blood & Lymphatic', index: 0 };
    const columnSoc = { name: 'Gastrointestinal', index: 1 };

    expect(getMapCell(new Map(), rowSoc, columnSoc)).toBeNull();
  });
});
