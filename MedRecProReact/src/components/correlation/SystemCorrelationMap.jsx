import { formatDecimal, formatInteger } from '../../lib/formatters';
import { getCorrelationColor, getScaleTextColor } from '../../lib/correlationScales';
import { EmptyState } from '../common/EmptyState';
import { CorrelationTooltip } from './CorrelationTooltip';
import { getAxisDensityClassName, shouldRenderAxisLabel } from './axisLabelDensity';
import { getSystemClassMapCell } from './correlationMapCells';

const MAP_ROW_LABEL_COLUMN = 'minmax(220px, 300px)';
const MAP_CELL_WIDTH = 84;

/**************************************************************/
/**
 * Builds a lookup that mirrors upper-triangle class correlation cells into a full grid.
 *
 * @param {object[]} cells - Normalized system correlation cells.
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
      rowClassCode: cell.columnClassCode,
      columnClassCode: cell.rowClassCode,
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
 * Formats long pharmacologic class names for compact axis labels.
 *
 * @param {string} value - Class name.
 * @returns {string} Compact axis label.
 */
function formatClassAxisLabel(value) {
  const cleanValue = String(value ?? '').replace(/\s*\[EPC\]\s*$/i, '').trim();

  return cleanValue.length > 30 ? `${cleanValue.slice(0, 27)}...` : cleanValue;
}

/**************************************************************/
/**
 * Pharmacologic class by class correlation matrix scoped to selected MedDRA systems.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Correlation map.
 */
export function SystemCorrelationMap({ map, selectedCell, onSelectCell }) {
  const classes = Array.isArray(map?.classes) ? map.classes : [];
  const cells = Array.isArray(map?.cells) ? map.cells : [];
  const cellLookup = buildMirroredCellLookup(cells);
  const selectedKey = selectedCell ? `${selectedCell.rowClassCode}|${selectedCell.columnClassCode}` : '';
  const appliedFilters = map?.appliedFilters ?? {};
  const methodText = formatMethod(appliedFilters.method);
  const aggregationText = formatAggregation(appliedFilters.aggregation);
  const minTermsPerCell = appliedFilters.minTermsPerCell ?? 4;
  const maxGridWidth = 300 + (classes.length * MAP_CELL_WIDTH) + (classes.length * 2);
  const densityClassName = getAxisDensityClassName(classes.length, { hideCellValues: true });
  const useFullAxisLabels = classes.length <= 32;

  if (classes.length === 0) {
    return (
      <>
        <EmptyState
          title="No class pairs available for these systems."
          body="Try relaxing the active filters or choosing another MedDRA system."
        />
        <CorrelationWarnings warnings={map?.warnings} />
      </>
    );
  }

  return (
    <div className="corr-panel">
      <div className="corr-wrap" role="region" aria-label="Class by class correlation map" tabIndex={0}>
        <div
          className={`corr-grid ${densityClassName}`.trim()}
          style={{
            '--corr-grid-max': `${maxGridWidth}px`,
            gridTemplateColumns: `${MAP_ROW_LABEL_COLUMN} repeat(${classes.length}, minmax(0, ${MAP_CELL_WIDTH}px))`,
          }}
          role="grid"
          aria-rowcount={classes.length + 1}
          aria-colcount={classes.length + 1}
        >
          <div className="corr-corner" role="columnheader" aria-label="Class" />
          {classes.map((columnClass, index) => {
            const renderLabel = shouldRenderAxisLabel(index, classes.length);
            const title = `${columnClass.pharmClassName} (${columnClass.pharmClassCode})`;

            return (
              <div
                className={`corr-colhead${renderLabel ? '' : ' is-suppressed'}`}
                key={`col-${columnClass.pharmClassCode}`}
                role="columnheader"
                aria-label={title}
                title={title}
              >
                {renderLabel ? (
                  <span>
                    {useFullAxisLabels ? columnClass.pharmClassName : formatClassAxisLabel(columnClass.pharmClassName)}
                  </span>
                ) : null}
              </div>
            );
          })}

          {classes.map((rowClass) => (
            <div className="corr-row-fragment" key={`row-${rowClass.pharmClassCode}`} role="row">
              <div className="corr-rowhead" role="rowheader">
                <span
                  className="corr-rowhead-name"
                  title={`${rowClass.pharmClassName} (${rowClass.pharmClassCode})`}
                >
                  {formatClassAxisLabel(rowClass.pharmClassName)}
                </span>
              </div>
              {classes.map((columnClass) => {
                const cell = getSystemClassMapCell(cellLookup, rowClass, columnClass);
                const backgroundColor = getCorrelationColor(cell?.coefficient);
                const enrichedCell = cell ? {
                  ...cell,
                  rowClass,
                  columnClass,
                  rowClassName: rowClass.pharmClassName,
                  columnClassName: columnClass.pharmClassName,
                } : null;
                const isSelected =
                  selectedKey === `${cell?.rowClassCode}|${cell?.columnClassCode}`
                  || selectedKey === `${cell?.columnClassCode}|${cell?.rowClassCode}`;
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
                    key={`${rowClass.pharmClassCode}-${columnClass.pharmClassCode}`}
                    type="button"
                    className={className}
                    role="gridcell"
                    aria-disabled={!isActionable}
                    aria-label={`${rowClass.pharmClassName} by ${columnClass.pharmClassName}: ${visibleValue || 'blank'}`}
                    style={{
                      backgroundColor: backgroundColor ?? undefined,
                      color: getScaleTextColor(cell?.coefficient),
                    }}
                    onClick={() => {
                      if (isActionable) {
                        onSelectCell(enrichedCell);
                      }
                    }}
                  >
                    <span className="corr-cell-value">{visibleValue}</span>
                    {cell ? (
                      <CorrelationTooltip>
                        <strong>{rowClass.pharmClassName} x {columnClass.pharmClassName}</strong>
                        <span>{rowClass.pharmClassCode} x {columnClass.pharmClassCode}</span>
                        <span>Coefficient: {visibleValue || 'blank'}</span>
                        <span>Shared terms: {formatInteger(cell.pairCount)}</span>
                        <span>p-value: {formatDecimal(cell.pValue, 3)}</span>
                        {cell.insufficientN ? <span>Below minimum terms-per-cell floor</span> : null}
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
        <span>{methodText} on selected-system term {aggregationText} - click a cell to drill in</span>
        <span className="corr-legend-muted">Blank = below the {minTermsPerCell}-term floor</span>
      </div>

      <CorrelationWarnings warnings={map?.warnings} />
    </div>
  );
}
