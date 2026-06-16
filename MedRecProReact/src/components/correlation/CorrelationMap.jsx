import { formatDecimal, formatInteger } from '../../lib/formatters';
import { getCorrelationColor, getScaleTextColor } from '../../lib/correlationScales';
import { EmptyState } from '../common/EmptyState';
import { CorrelationTooltip } from './CorrelationTooltip';
import { formatSocAxisLabel } from './socLabels';

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
 * Keeps only SOC axes with at least one usable off-diagonal coefficient.
 *
 * @param {string[]} soc - SOC names from the API axis.
 * @param {object[]} cells - Normalized map cells.
 * @returns {{ name: string, index: number }[]} Display SOC entries.
 */
function getDisplaySocEntries(soc, cells) {
  const activeIndexes = new Set();

  for (const cell of cells) {
    if (cell && !cell.isDiagonal && cell.coefficient !== null && !cell.insufficientN) {
      activeIndexes.add(cell.rowIndex);
      activeIndexes.add(cell.columnIndex);
    }
  }

  const entries = soc.map((name, index) => ({ name, index }));

  return activeIndexes.size > 0
    ? entries.filter((entry) => activeIndexes.has(entry.index))
    : entries;
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
    return '';
  }

  return formatDecimal(cell.coefficient, 2);
}

/**************************************************************/
/**
 * Formats the correlation method echoed by the backend.
 *
 * @param {string | undefined} method - Applied correlation method.
 * @returns {string} Human-readable method name.
 */
function formatMethod(method) {
  return method === 'Pearson' ? 'Pearson' : 'Spearman';
}

/**************************************************************/
/**
 * Formats the LogRR aggregation echoed by the backend.
 *
 * @param {string | undefined} aggregation - Applied aggregation token.
 * @returns {string} Human-readable aggregation name.
 */
function formatAggregation(aggregation) {
  return aggregation === 'MeanLogRr' ? 'mean LogRR' : 'median LogRR';
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
  const displaySoc = getDisplaySocEntries(soc, cells);
  const cellLookup = buildMirroredCellLookup(cells);
  const selectedKey = selectedCell ? `${selectedCell.rowSoc}|${selectedCell.columnSoc}` : '';
  const appliedFilters = map?.appliedFilters ?? {};
  const methodText = formatMethod(appliedFilters.method);
  const aggregationText = formatAggregation(appliedFilters.aggregation);
  const minDrugsPerCell = appliedFilters.minDrugsPerCell ?? 4;
  const maxGridWidth = 220 + (displaySoc.length * 84) + (displaySoc.length * 2);

  if (displaySoc.length === 0) {
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
      <div className="corr-wrap" role="region" aria-label="SOC by SOC correlation map" tabIndex={0}>
        <div
          className="corr-grid"
          style={{
            '--corr-grid-max': `${maxGridWidth}px`,
            gridTemplateColumns: `minmax(120px, 220px) repeat(${displaySoc.length}, minmax(0, 84px))`,
          }}
          role="grid"
          aria-rowcount={displaySoc.length + 1}
          aria-colcount={displaySoc.length + 1}
        >
          <div className="corr-corner" role="columnheader" aria-label="SOC" />
          {displaySoc.map((columnSoc) => (
            <div className="corr-colhead" key={`col-${columnSoc.name}`} role="columnheader">
              <span title={columnSoc.name}>{formatSocAxisLabel(columnSoc.name)}</span>
            </div>
          ))}

          {displaySoc.map((rowSoc) => (
            <div className="corr-row-fragment" key={`row-${rowSoc.name}`} role="row">
              <div className="corr-rowhead" role="rowheader">
                <span className="corr-rowhead-name" title={rowSoc.name}>{formatSocAxisLabel(rowSoc.name)}</span>
              </div>
              {displaySoc.map((columnSoc) => {
                const cell = cellLookup.get(`${rowSoc.index}:${columnSoc.index}`) ?? null;
                const backgroundColor = getCorrelationColor(cell?.coefficient);
                const isSelected =
                  selectedKey === `${cell?.rowSoc}|${cell?.columnSoc}`
                  || selectedKey === `${cell?.columnSoc}|${cell?.rowSoc}`;
                const isActionable = Boolean(cell && !cell.isDiagonal);
                const visibleValue = formatCoefficient(cell);
                const className = [
                  'corr-cell',
                  cell?.isDiagonal ? 'diag' : '',
                  cell?.insufficientN ? 'thin empty' : '',
                  !backgroundColor ? 'empty' : '',
                  isSelected ? 'selected' : '',
                ].filter(Boolean).join(' ');

                return (
                  <button
                    key={`${rowSoc.name}-${columnSoc.name}`}
                    type="button"
                    className={className}
                    role="gridcell"
                    aria-disabled={!isActionable}
                    aria-label={`${rowSoc.name} by ${columnSoc.name}: ${visibleValue || 'blank'}`}
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
                    <span className="corr-cell-value">{visibleValue}</span>
                    {cell ? (
                      <CorrelationTooltip>
                        <strong>{cell.rowSoc} x {cell.columnSoc}</strong>
                        <span>Coefficient: {visibleValue || 'blank'}</span>
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
        <span className="corr-legend-scale">
          <span className="corr-legend-label">-1</span>
          <span className="corr-legend-ramp" />
          <span className="corr-legend-label">+1</span>
        </span>
        <span>{methodText} on per-drug {aggregationText} - click a cell to drill in</span>
        <span className="corr-legend-muted">Blank = below the {minDrugsPerCell}-drug floor</span>
      </div>

      <CorrelationWarnings warnings={map?.warnings} />
    </div>
  );
}
