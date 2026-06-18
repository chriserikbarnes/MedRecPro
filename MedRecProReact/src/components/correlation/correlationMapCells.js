/**************************************************************/
/**
 * Reads a rendered map cell, synthesizing missing diagonal cells for sparse payloads.
 *
 * @param {Map<string, object>} cellLookup - Mirrored cell lookup.
 * @param {{ name: string, index: number }} rowSoc - Row SOC entry.
 * @param {{ name: string, index: number }} columnSoc - Column SOC entry.
 * @returns {object | null} Existing cell, synthetic diagonal, or null.
 */
export function getMapCell(cellLookup, rowSoc, columnSoc) {
  const cell = cellLookup.get(`${rowSoc.index}:${columnSoc.index}`) ?? null;

  if (cell || rowSoc.index !== columnSoc.index) {
    return cell;
  }

  return {
    rowIndex: rowSoc.index,
    columnIndex: columnSoc.index,
    rowSoc: rowSoc.name,
    columnSoc: columnSoc.name,
    coefficient: 1,
    pairCount: 0,
    pValue: null,
    isSignificant: false,
    isFragile: false,
    insufficientN: false,
    isDiagonal: true,
  };
}

/**************************************************************/
/**
 * Reads a rendered system class-pair cell, synthesizing sparse diagonal cells.
 *
 * @param {Map<string, object>} cellLookup - Mirrored cell lookup.
 * @param {{ pharmClassName: string, pharmClassCode: string, index: number }} rowClass - Row class entry.
 * @param {{ pharmClassName: string, pharmClassCode: string, index: number }} columnClass - Column class entry.
 * @returns {object | null} Existing cell, synthetic diagonal, or null.
 */
export function getSystemClassMapCell(cellLookup, rowClass, columnClass) {
  const cell = cellLookup.get(`${rowClass.index}:${columnClass.index}`) ?? null;

  if (cell || rowClass.index !== columnClass.index) {
    return cell;
  }

  return {
    rowIndex: rowClass.index,
    columnIndex: columnClass.index,
    rowClassCode: rowClass.pharmClassCode,
    columnClassCode: columnClass.pharmClassCode,
    coefficient: 1,
    pairCount: 0,
    pValue: null,
    isSignificant: false,
    isFragile: false,
    insufficientN: false,
    isDiagonal: true,
  };
}
