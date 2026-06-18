import { formatDecimal, formatInteger } from '../../lib/formatters';
import { getLogRrColor, getScaleTextColor } from '../../lib/correlationScales';
import { EmptyState } from '../common/EmptyState';
import { CorrelationTooltip } from './CorrelationTooltip';

/**************************************************************/
/**
 * Builds a lookup for sparse system heatmap cells.
 *
 * @param {object[]} cells - Normalized heatmap cells.
 * @returns {Map<string, object>} Heatmap cell lookup.
 */
function buildHeatmapCellLookup(cells) {
  const lookup = new Map();

  for (const cell of cells) {
    lookup.set(`${cell.classIndex}:${cell.drugIndex}`, cell);
  }

  return lookup;
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
 * Renders heatmap warning text.
 *
 * @param {{ warnings?: string[] }} props - Component props.
 * @returns {JSX.Element | null} Warning block or null.
 */
function HeatmapWarnings({ warnings = [] }) {
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
 * Sparse pharmacologic-class by drug LogRR heatmap scoped to selected systems.
 *
 * @param {{ heatmap: object | null }} props - Component props.
 * @returns {JSX.Element} Heatmap.
 */
export function SystemCorrelationHeatmap({ heatmap }) {
  const classes = Array.isArray(heatmap?.classes) ? heatmap.classes : [];
  const drugs = Array.isArray(heatmap?.drugs) ? heatmap.drugs : [];
  const cells = Array.isArray(heatmap?.cells) ? heatmap.cells : [];
  const cellLookup = buildHeatmapCellLookup(cells);
  const aggregationText = formatAggregation(heatmap?.appliedFilters?.aggregation);
  const totalDrugCount = heatmap?.drugPage?.totalCount || drugs.length;
  const maxGridWidth = 220 + (drugs.length * 64) + (drugs.length * 2);

  if (classes.length === 0 || drugs.length === 0) {
    return (
      <>
        <EmptyState
          title="No heatmap cells available for these systems."
          body="Try relaxing the active filters or choosing another MedDRA system."
        />
        <HeatmapWarnings warnings={heatmap?.warnings} />
      </>
    );
  }

  return (
    <div className="corr-panel">
      <div className="corr-wrap" role="region" aria-label="Class by drug LogRR heatmap" tabIndex={0}>
        <div
          className="corr-grid heat"
          style={{
            '--corr-grid-max': `${maxGridWidth}px`,
            gridTemplateColumns: `minmax(120px, 220px) repeat(${drugs.length}, minmax(0, 64px))`,
          }}
          role="grid"
          aria-rowcount={classes.length + 1}
          aria-colcount={drugs.length + 1}
        >
          <div className="corr-corner" role="columnheader" aria-label="Class" />
          {drugs.map((drug) => (
            <div className="corr-colhead drug" key={drug.id} role="columnheader">
              <span title={drug.drugDisplayName}>{drug.drugDisplayName}</span>
            </div>
          ))}

          {classes.map((rowClass) => (
            <div className="heatmap-row-fragment" key={rowClass.pharmClassCode} role="row">
              <div className="corr-rowhead" role="rowheader">
                <span
                  className="corr-rowhead-name"
                  title={`${rowClass.pharmClassName} (${rowClass.pharmClassCode})`}
                >
                  {formatClassAxisLabel(rowClass.pharmClassName)}
                </span>
              </div>
              {drugs.map((drug) => {
                const cell = cellLookup.get(`${rowClass.index}:${drug.index}`) ?? null;
                const backgroundColor = getLogRrColor(cell?.logRr);
                const className = [
                  'corr-cell',
                  !backgroundColor ? 'empty' : '',
                ].filter(Boolean).join(' ');

                return (
                  <div
                    key={`${rowClass.pharmClassCode}-${drug.id}`}
                    className={className}
                    role="gridcell"
                    tabIndex={0}
                    aria-label={`${rowClass.pharmClassName} by ${drug.drugDisplayName}: LogRR ${cell ? formatDecimal(cell.logRr, 2) : 'blank'}`}
                    style={{
                      backgroundColor: backgroundColor ?? undefined,
                      color: getScaleTextColor(cell?.logRr),
                    }}
                  >
                    {cell ? (
                      <CorrelationTooltip>
                        <strong>{rowClass.pharmClassName} x {drug.drugDisplayName}</strong>
                        <span>LogRR: {formatDecimal(cell.logRr, 2)}</span>
                        <span>RR: {formatDecimal(cell.rr, 2)}</span>
                        <span>Significance: {cell.significance}</span>
                        <span>Precision: {cell.precision}</span>
                        <span>Terms: {formatInteger(cell.termCount)}</span>
                      </CorrelationTooltip>
                    ) : null}
                  </div>
                );
              })}
            </div>
          ))}
        </div>
      </div>

      <div className="corr-legend" aria-label="Heatmap color legend">
        <span className="corr-legend-label">protective</span>
        <span className="corr-legend-ramp" />
        <span className="corr-legend-label">elevated</span>
        <span>Cell = per-drug selected-system {aggregationText} - blank = no AE rows</span>
        <span className="corr-legend-muted">
          Showing {formatInteger(drugs.length)} of {formatInteger(totalDrugCount)} drugs
        </span>
      </div>

      <HeatmapWarnings warnings={heatmap?.warnings} />
    </div>
  );
}
