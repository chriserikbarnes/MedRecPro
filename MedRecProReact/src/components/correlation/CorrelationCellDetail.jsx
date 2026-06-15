import { formatDecimal, formatInteger } from '../../lib/formatters';
import { EmptyState } from '../common/EmptyState';
import { InlineError } from '../common/InlineError';
import { Loading } from '../common/Loading';

/**************************************************************/
/**
 * Finds the finite domain for a scatter axis.
 *
 * @param {number[]} values - Numeric values.
 * @returns {{ min: number, max: number }} Axis domain.
 */
function getScatterDomain(values) {
  const finiteValues = values.filter((value) => Number.isFinite(value));

  if (finiteValues.length === 0) {
    return { min: -1, max: 1 };
  }

  const min = Math.min(...finiteValues);
  const max = Math.max(...finiteValues);
  const spread = Math.max(0.5, max - min);
  const padding = spread * 0.15;

  return {
    min: min - padding,
    max: max + padding,
  };
}

/**************************************************************/
/**
 * Converts a domain value into an SVG coordinate.
 *
 * @param {number} value - Data value.
 * @param {{ min: number, max: number }} domain - Axis domain.
 * @param {number} low - Low output coordinate.
 * @param {number} high - High output coordinate.
 * @returns {number} Coordinate.
 */
function scalePoint(value, domain, low, high) {
  if (!Number.isFinite(value) || domain.max === domain.min) {
    return (low + high) / 2;
  }

  const ratio = (value - domain.min) / (domain.max - domain.min);

  return low + (ratio * (high - low));
}

/**************************************************************/
/**
 * Renders the per-drug LogRR scatter plot.
 *
 * @param {{ pairs: object[] }} props - Component props.
 * @returns {JSX.Element} Scatter plot.
 */
function PairScatter({ pairs }) {
  const drawablePairs = pairs.filter((pair) => Number.isFinite(pair.logRrX) && Number.isFinite(pair.logRrY));
  const xDomain = getScatterDomain(drawablePairs.map((pair) => pair.logRrX));
  const yDomain = getScatterDomain(drawablePairs.map((pair) => pair.logRrY));

  return (
    <svg className="cell-scatter" viewBox="0 0 320 220" role="img" aria-label="Per-drug LogRR scatter plot">
      <line className="scatter-axis" x1="36" y1="184" x2="292" y2="184" />
      <line className="scatter-axis" x1="36" y1="28" x2="36" y2="184" />
      <line
        className="scatter-zero"
        x1={scalePoint(0, xDomain, 36, 292)}
        y1="28"
        x2={scalePoint(0, xDomain, 36, 292)}
        y2="184"
      />
      <line
        className="scatter-zero"
        x1="36"
        y1={scalePoint(0, yDomain, 184, 28)}
        x2="292"
        y2={scalePoint(0, yDomain, 184, 28)}
      />
      {drawablePairs.map((pair) => (
        <circle
          key={`${pair.drugDisplayName}-${pair.logRrX}-${pair.logRrY}`}
          className="scatter-point"
          cx={scalePoint(pair.logRrX, xDomain, 36, 292)}
          cy={scalePoint(pair.logRrY, yDomain, 184, 28)}
          r="5"
        >
          <title>{pair.drugDisplayName}: {formatDecimal(pair.logRrX, 2)}, {formatDecimal(pair.logRrY, 2)}</title>
        </circle>
      ))}
      <text className="scatter-label" x="164" y="210">SOC X LogRR</text>
      <text className="scatter-label y" x="-108" y="13">SOC Y LogRR</text>
    </svg>
  );
}

/**************************************************************/
/**
 * Renders a compact metric card in cell detail.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Metric card.
 */
function CellMetric({ label, value, sub = '' }) {
  return (
    <article className="cell-metric">
      <span>{label}</span>
      <strong>{value}</strong>
      {sub ? <small>{sub}</small> : null}
    </article>
  );
}

/**************************************************************/
/**
 * Renders warnings returned by the cell endpoint.
 *
 * @param {{ warnings?: string[] }} props - Component props.
 * @returns {JSX.Element | null} Warning block or null.
 */
function CellWarnings({ warnings = [] }) {
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
 * Drill-down panel for a selected correlation map cell.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element | null} Cell detail panel or null.
 */
export function CorrelationCellDetail({ selectedCell, detail, isLoading, error, onRetry }) {
  if (!selectedCell) {
    return (
      <section className="panel corr-detail-panel">
        <EmptyState title="Select an off-diagonal map cell." body="Cell detail will show paired per-drug LogRR values." />
      </section>
    );
  }

  return (
    <section className="panel corr-detail-panel">
      <div className="panel-header">
        <div className="panel-heading">
          <div className="panel-title">{selectedCell.rowSoc} x {selectedCell.columnSoc}</div>
          <div className="panel-sub">Per-drug paired observations behind the selected map cell.</div>
        </div>
      </div>

      {isLoading ? <Loading label="Loading cell detail" /> : null}
      {error ? <InlineError error={error} onRetry={onRetry} /> : null}
      {!isLoading && !error && detail ? (
        <div className="cell-detail">
          <div className="cell-metrics">
            <CellMetric
              label="Map-safe coefficient"
              value={formatDecimal(detail.coefficient, 2)}
              sub={detail.insufficientN ? 'Suppressed below floor' : 'Rendered on map'}
            />
            <CellMetric
              label="Raw diagnostic coefficient"
              value={formatDecimal(detail.rawCoefficient, 2)}
              sub="Unsuppressed pairwise value"
            />
            <CellMetric
              label="Pairs"
              value={formatInteger(detail.pairCount)}
              sub={`Floor ${formatInteger(detail.minDrugsPerCell)}`}
            />
            <CellMetric
              label="p-values"
              value={formatDecimal(detail.pValue, 3)}
              sub={`Raw ${formatDecimal(detail.rawPValue, 3)}`}
            />
          </div>

          <div className="cell-detail-grid">
            <div className="cell-scatter-wrap">
              <PairScatter pairs={detail.drugPairs} />
            </div>

            <div className="cell-pair-list" role="table" aria-label="Per-drug paired LogRR values">
              <div className="cell-pair-row is-head" role="row">
                <span role="columnheader">Drug</span>
                <span role="columnheader">{detail.socX}</span>
                <span role="columnheader">{detail.socY}</span>
              </div>
              {detail.drugPairs.map((pair) => (
                <div className="cell-pair-row" key={pair.drugDisplayName} role="row">
                  <span role="cell" title={pair.drugDisplayName}>{pair.drugDisplayName}</span>
                  <span role="cell">
                    RR {formatDecimal(pair.rrX, 2)}
                    <small>Log {formatDecimal(pair.logRrX, 2)} | {pair.precisionX} | {formatInteger(pair.termCountX)} terms</small>
                  </span>
                  <span role="cell">
                    RR {formatDecimal(pair.rrY, 2)}
                    <small>Log {formatDecimal(pair.logRrY, 2)} | {pair.precisionY} | {formatInteger(pair.termCountY)} terms</small>
                  </span>
                </div>
              ))}
            </div>
          </div>

          <CellWarnings warnings={detail.warnings} />
        </div>
      ) : null}
    </section>
  );
}
