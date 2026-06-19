import { formatDecimal, formatInteger } from '../../lib/formatters';
import { getLogRrColor, getScaleTextColor } from '../../lib/correlationScales';
import { EmptyState } from '../common/EmptyState';
import { CorrelationTooltip } from './CorrelationTooltip';
import { formatSocAxisLabel } from './socLabels';

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
 * Removes leading/trailing empty SOC rows and drug columns when sparse data allows.
 *
 * @param {string[]} soc - SOC names from the API axis.
 * @param {object[]} drugs - Drug axis entries.
 * @param {object[]} cells - Normalized heatmap cells.
 * @returns {{ soc: { name: string, index: number }[], drugs: { drug: object, index: number }[] }} Display axes.
 */
function getDisplayHeatmapAxes(soc, drugs, cells) {
  const activeSocIndexes = new Set();
  const activeDrugIndexes = new Set();

  for (const cell of cells) {
    if (cell && cell.logRr !== null && cell.logRr !== undefined) {
      activeSocIndexes.add(cell.socIndex);
      activeDrugIndexes.add(cell.drugIndex);
    }
  }

  const socEntries = soc.map((name, index) => ({ name, index }));
  const drugEntries = drugs.map((drug, index) => ({ drug, index }));

  return {
    soc: activeSocIndexes.size > 0
      ? socEntries.filter((entry) => activeSocIndexes.has(entry.index))
      : socEntries,
    drugs: activeDrugIndexes.size > 0
      ? drugEntries.filter((entry) => activeDrugIndexes.has(entry.index))
      : drugEntries,
  };
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
  const displayAxes = getDisplayHeatmapAxes(soc, drugs, cells);
  const cellLookup = buildHeatmapCellLookup(cells);
  const aggregationText = formatAggregation(heatmap?.appliedFilters?.aggregation);
  const totalDrugCount = heatmap?.drugCount || drugs.length;

  if (displayAxes.soc.length === 0 || displayAxes.drugs.length === 0) {
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
      <div className="corr-wrap" role="region" aria-label="SOC by drug LogRR heatmap" tabIndex={0}>
        <div
          className="corr-grid heat"
          style={{
            gridTemplateColumns: `minmax(120px, 220px) repeat(${displayAxes.drugs.length}, minmax(0, var(--corr-cell-max)))`,
          }}
          role="grid"
          aria-rowcount={displayAxes.soc.length + 1}
          aria-colcount={displayAxes.drugs.length + 1}
        >
          <div className="corr-corner" role="columnheader" aria-label="SOC" />
          {displayAxes.drugs.map(({ drug }) => (
            <div className="corr-colhead drug" key={drug.drugDisplayName} role="columnheader">
              <span title={drug.drugDisplayName}>{drug.drugDisplayName}</span>
            </div>
          ))}

          {displayAxes.soc.map((rowSoc) => (
            <div className="heatmap-row-fragment" key={rowSoc.name} role="row">
              <div className="corr-rowhead" role="rowheader">
                <span className="corr-rowhead-name" title={rowSoc.name}>{formatSocAxisLabel(rowSoc.name)}</span>
              </div>
              {displayAxes.drugs.map(({ drug, index: drugIndex }) => {
                const cell = cellLookup.get(`${rowSoc.index}:${drugIndex}`) ?? null;
                const backgroundColor = getLogRrColor(cell?.logRr);
                const className = [
                  'corr-cell',
                  !backgroundColor ? 'empty' : '',
                ].filter(Boolean).join(' ');

                return (
                  <div
                    key={`${rowSoc.name}-${drug.drugDisplayName}`}
                    className={className}
                    role="gridcell"
                    tabIndex={0}
                    aria-label={`${rowSoc.name} by ${drug.drugDisplayName}: LogRR ${cell ? formatDecimal(cell.logRr, 2) : 'blank'}`}
                    style={{
                      backgroundColor: backgroundColor ?? undefined,
                      color: getScaleTextColor(cell?.logRr),
                    }}
                  >
                    {cell ? (
                      <CorrelationTooltip>
                        <strong>{rowSoc.name} x {drug.drugDisplayName}</strong>
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
        <span>Cell = per-drug {aggregationText} in that SOC - blank = no AE rows</span>
        <span className="corr-legend-muted">
          Showing densest {displayAxes.drugs.length} of {totalDrugCount} drugs
        </span>
      </div>

      <HeatmapWarnings warnings={heatmap?.warnings} />
    </div>
  );
}
