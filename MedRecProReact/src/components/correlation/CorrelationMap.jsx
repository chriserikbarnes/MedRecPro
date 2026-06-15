import { formatDecimal, formatInteger } from '../../lib/formatters';
import { getCorrelationColor, getScaleTextColor } from '../../lib/correlationScales';
import { EmptyState } from '../common/EmptyState';
import { CorrelationTooltip } from './CorrelationTooltip';

/**************************************************************/
/**
 * Builds a lookup that mirrors upper-triangle correlation cells into a full grid.
 *
 * @param {object[]} cells - Normalized correlation cells.
 * @returns {Map<string, object>} Cell lookup.
 */
function buildMirroredCellLookup(cells) {
  const lookup = new Map();

  for (const cell of cells) {
    lookup.set(`${cell.rowIndex}:${cell.columnIndex}`, cell);
    lookup.set(`${cell.columnIndex}:${cell.rowIndex}`, {
      ...cell,
      rowIndex: cell.columnIndex,
      columnIndex: cell.rowIndex,
      rowSoc: cell.columnSoc,
      columnSoc: cell.rowSoc,
    });
  }

  return lookup;
}

/**************************************************************/
/**
 * Gets display text for one coefficient cell.
 *
 * @param {object | null} cell - Correlation cell.
 * @returns {string} Display label.
 */
function formatCoefficient(cell) {
  if (!cell) {
    return '';
  }

  if (cell.isDiagonal) {
    return '1';
  }

  if (cell.coefficient === null) {
    return '-';
  }

  return formatDecimal(cell.coefficient, 2);
}

/**************************************************************/
/**
 * Renders backend warnings near the visual they qualify.
 *
 * @param {{ warnings?: string[] }} props - Component props.
 * @returns {JSX.Element | null} Warning block or null.
 */
function CorrelationWarnings({ warnings = [] }) {
  if (!Array.isArray(warnings) || warnings.length === 0) {
    return null;
  }

  return (
    <div className="corr-warning" role="note">
      {warnings.map((warning) => (
        <p key={warning}>{warning}</p>
      ))}
    </div>
  );
}

/**************************************************************/
/**
 * SOC by SOC correlation matrix.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Correlation map.
 */
export function CorrelationMap({ map, selectedCell, onSelectCell }) {
  const soc = Array.isArray(map?.soc) ? map.soc : [];
  const cells = Array.isArray(map?.cells) ? map.cells : [];
  const cellLookup = buildMirroredCellLookup(cells);
  const selectedKey = selectedCell ? `${selectedCell.rowSoc}|${selectedCell.columnSoc}` : '';

  if (soc.length === 0) {
    return (
      <>
        <EmptyState
          title="No SOC pairs available for this class."
          body="Try the heatmap or relax the active filters."
        />
        <CorrelationWarnings warnings={map?.warnings} />
      </>
    );
  }

  return (
    <div className="corr-panel">
      <div className="corr-grid-wrap" role="region" aria-label="SOC by SOC correlation map" tabIndex={0}>
        <div
          className="corr-map-grid"
          style={{ gridTemplateColumns: `minmax(160px, 220px) repeat(${soc.length}, 58px)` }}
          role="grid"
          aria-rowcount={soc.length + 1}
          aria-colcount={soc.length + 1}
        >
          <div className="corr-axis-cell corr-corner" role="columnheader">SOC</div>
          {soc.map((columnSoc) => (
            <div className="corr-axis-cell corr-col-head" key={`col-${columnSoc}`} role="columnheader">
              <span title={columnSoc}>{columnSoc}</span>
            </div>
          ))}

          {soc.map((rowSoc, rowIndex) => (
            <div className="corr-row-fragment" key={`row-${rowSoc}`} role="row">
              <div className="corr-axis-cell corr-row-head" role="rowheader">
                <span title={rowSoc}>{rowSoc}</span>
              </div>
              {soc.map((columnSoc, columnIndex) => {
                const cell = cellLookup.get(`${rowIndex}:${columnIndex}`) ?? null;
                const backgroundColor = getCorrelationColor(cell?.coefficient);
                const isSelected =
                  selectedKey === `${cell?.rowSoc}|${cell?.columnSoc}`
                  || selectedKey === `${cell?.columnSoc}|${cell?.rowSoc}`;
                const isActionable = Boolean(cell && !cell.isDiagonal);

                return (
                  <button
                    key={`${rowSoc}-${columnSoc}`}
                    type="button"
                    className={`corr-cell${cell?.isDiagonal ? ' is-diagonal' : ''}${cell?.insufficientN ? ' is-thin' : ''}${!backgroundColor ? ' is-empty' : ''}${isSelected ? ' is-selected' : ''}`}
                    role="gridcell"
                    aria-disabled={!isActionable}
                    aria-label={`${rowSoc} by ${columnSoc}: ${formatCoefficient(cell)}`}
                    style={{
                      backgroundColor: backgroundColor ?? undefined,
                      color: getScaleTextColor(cell?.coefficient),
                    }}
                    onClick={() => {
                      if (isActionable) {
                        onSelectCell(cell);
                      }
                    }}
                  >
                    <span className="corr-cell-value">{formatCoefficient(cell)}</span>
                    {cell ? (
                      <CorrelationTooltip>
                        <strong>{cell.rowSoc} x {cell.columnSoc}</strong>
                        <span>Coefficient: {formatCoefficient(cell)}</span>
                        <span>Pairs: {formatInteger(cell.pairCount)}</span>
                        <span>p-value: {formatDecimal(cell.pValue, 3)}</span>
                        {cell.insufficientN ? <span>Below minimum drugs-per-cell floor</span> : null}
                        {cell.isDiagonal ? <span>Diagonal cell is non-informative</span> : null}
                      </CorrelationTooltip>
                    ) : null}
                  </button>
                );
              })}
            </div>
          ))}
        </div>
      </div>

      <div className="corr-legend" aria-label="Correlation color legend">
        <span className="corr-legend-swatch negative" />
        <span>Protective alignment</span>
        <span className="corr-legend-line" />
        <span>Neutral</span>
        <span className="corr-legend-swatch positive" />
        <span>Elevated alignment</span>
      </div>

      <CorrelationWarnings warnings={map?.warnings} />
    </div>
  );
}
