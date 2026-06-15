import { formatDecimal, formatInteger } from '../../lib/formatters';
import { getLogRrColor, getScaleTextColor } from '../../lib/correlationScales';
import { EmptyState } from '../common/EmptyState';
import { CorrelationTooltip } from './CorrelationTooltip';

/**************************************************************/
/**
 * Builds a lookup for sparse heatmap cells.
 *
 * @param {object[]} cells - Normalized heatmap cells.
 * @returns {Map<string, object>} Heatmap cell lookup.
 */
function buildHeatmapCellLookup(cells) {
  const lookup = new Map();

  for (const cell of cells) {
    lookup.set(`${cell.socIndex}:${cell.drugIndex}`, cell);
  }

  return lookup;
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
 * Sparse SOC by drug LogRR heatmap.
 *
 * @param {{ heatmap: object | null }} props - Component props.
 * @returns {JSX.Element} Heatmap.
 */
export function CorrelationHeatmap({ heatmap }) {
  const soc = Array.isArray(heatmap?.soc) ? heatmap.soc : [];
  const drugs = Array.isArray(heatmap?.drugs) ? heatmap.drugs : [];
  const cells = Array.isArray(heatmap?.cells) ? heatmap.cells : [];
  const cellLookup = buildHeatmapCellLookup(cells);

  if (soc.length === 0 || drugs.length === 0) {
    return (
      <>
        <EmptyState
          title="No heatmap cells available for this class."
          body="Try relaxing the active filters or choosing another class."
        />
        <HeatmapWarnings warnings={heatmap?.warnings} />
      </>
    );
  }

  return (
    <div className="corr-panel">
      <div className="heatmap-wrap" role="region" aria-label="SOC by drug LogRR heatmap" tabIndex={0}>
        <div
          className="heatmap-grid"
          style={{ gridTemplateColumns: `minmax(160px, 220px) repeat(${drugs.length}, 52px)` }}
          role="grid"
          aria-rowcount={soc.length + 1}
          aria-colcount={drugs.length + 1}
        >
          <div className="heatmap-axis-cell heatmap-corner" role="columnheader">SOC</div>
          {drugs.map((drug) => (
            <div className="heatmap-axis-cell heatmap-col-head" key={drug.drugDisplayName} role="columnheader">
              <span title={drug.drugDisplayName}>{drug.drugDisplayName}</span>
            </div>
          ))}

          {soc.map((rowSoc, socIndex) => (
            <div className="heatmap-row-fragment" key={rowSoc} role="row">
              <div className="heatmap-axis-cell heatmap-row-head" role="rowheader">
                <span title={rowSoc}>{rowSoc}</span>
              </div>
              {drugs.map((drug, drugIndex) => {
                const cell = cellLookup.get(`${socIndex}:${drugIndex}`) ?? null;
                const backgroundColor = getLogRrColor(cell?.logRr);

                return (
                  <div
                    key={`${rowSoc}-${drug.drugDisplayName}`}
                    className={`heatmap-cell${cell ? '' : ' is-empty'}`}
                    role="gridcell"
                    tabIndex={0}
                    aria-label={`${rowSoc} by ${drug.drugDisplayName}: LogRR ${formatDecimal(cell?.logRr, 2)}`}
                    style={{
                      backgroundColor: backgroundColor ?? undefined,
                      color: getScaleTextColor(cell?.logRr),
                    }}
                  >
                    <span className="heatmap-cell-value">{cell ? formatDecimal(cell.logRr, 2) : ''}</span>
                    {cell ? (
                      <CorrelationTooltip>
                        <strong>{rowSoc} x {drug.drugDisplayName}</strong>
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
        <span className="corr-legend-swatch negative" />
        <span>Lower LogRR</span>
        <span className="corr-legend-line" />
        <span>Neutral</span>
        <span className="corr-legend-swatch positive" />
        <span>Higher LogRR</span>
      </div>

      <HeatmapWarnings warnings={heatmap?.warnings} />
    </div>
  );
}
