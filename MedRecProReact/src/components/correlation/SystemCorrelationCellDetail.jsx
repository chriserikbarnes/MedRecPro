import { formatCountLabel, formatDecimal, formatInteger } from '../../lib/formatters';
import { EmptyState } from '../common/EmptyState';
import { InlineError } from '../common/InlineError';
import { Loading } from '../common/Loading';
import { CorrelationPager } from './CorrelationPager';
import { getScatterDomain, scalePoint } from './correlationScatter';

/**************************************************************/
/**
 * Renders the selected-system term LogRR scatter plot.
 *
 * @param {{ pairs: object[], classXLabel: string, classYLabel: string }} props - Component props.
 * @returns {JSX.Element} Scatter plot.
 */
function TermPairScatter({ pairs, classXLabel, classYLabel }) {
  const drawablePairs = pairs.filter((pair) => Number.isFinite(pair.logRrX) && Number.isFinite(pair.logRrY));
  const xDomain = getScatterDomain(drawablePairs.map((pair) => pair.logRrX));
  const yDomain = getScatterDomain(drawablePairs.map((pair) => pair.logRrY));

  return (
    <svg className="cell-scatter" viewBox="0 0 320 220" role="img" aria-label="Selected-system term LogRR scatter plot">
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
          key={`${pair.systemOrganClass}-${pair.parameterName}-${pair.logRrX}-${pair.logRrY}`}
          className="scatter-point"
          cx={scalePoint(pair.logRrX, xDomain, 36, 292)}
          cy={scalePoint(pair.logRrY, yDomain, 184, 28)}
          r="5"
        >
          <title>{pair.parameterName}: {formatDecimal(pair.logRrX, 2)}, {formatDecimal(pair.logRrY, 2)}</title>
        </circle>
      ))}
      <text className="scatter-label" x="164" y="210">{classXLabel} LogRR</text>
      <text className="scatter-label y" x="-108" y="13">{classYLabel} LogRR</text>
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
 * Renders warnings returned by the system cell endpoint.
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
 * Drill-down panel for a selected system-scoped class-pair map cell.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element | null} Cell detail panel or null.
 */
export function SystemCorrelationCellDetail({
  selectedCell,
  detail,
  isLoading,
  error,
  onRetry,
  onChangeTermPairPage,
  onChangeTermPairPageSize,
}) {
  if (!selectedCell) {
    return (
      <section className="panel corr-detail-panel">
        <EmptyState
          title="Select an off-diagonal class-pair cell."
          body="Cell detail will show shared selected-system adverse-event terms."
        />
      </section>
    );
  }

  const classXLabel = detail?.classX?.pharmClassName ?? selectedCell.rowClassName ?? selectedCell.rowClassCode;
  const classYLabel = detail?.classY?.pharmClassName ?? selectedCell.columnClassName ?? selectedCell.columnClassCode;

  return (
    <section className="panel corr-detail-panel">
      <div className="panel-header">
        <div className="panel-heading">
          <div className="panel-title">{classXLabel} x {classYLabel}</div>
          <div className="panel-sub">Shared selected-system terms behind the selected class-pair cell.</div>
        </div>
      </div>

      {isLoading ? <Loading label="Loading term detail" /> : null}
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
              sub="Unsuppressed term-pair value"
            />
            <CellMetric
              label="Term pairs"
              value={formatInteger(detail.pairCount)}
              sub={`Floor ${formatInteger(detail.minTermsPerCell)}`}
            />
            <CellMetric
              label="p-values"
              value={formatDecimal(detail.pValue, 3)}
              sub={`Raw ${formatDecimal(detail.rawPValue, 3)}`}
            />
          </div>

          <CorrelationPager
            label="Term pairs"
            page={detail.termPairPage}
            itemLabel="term pairs"
            pageSizeOptions={[50, 100, 250, 500]}
            onChangePage={onChangeTermPairPage}
            onChangePageSize={onChangeTermPairPageSize}
          />

          <div className="cell-detail-grid system-cell-detail-grid">
            <div className="cell-scatter-wrap">
              <TermPairScatter
                pairs={detail.termPairs}
                classXLabel={detail.classX?.pharmClassCode ?? selectedCell.rowClassCode}
                classYLabel={detail.classY?.pharmClassCode ?? selectedCell.columnClassCode}
              />
            </div>

            <div className="cell-pair-list system-term-pair-list" role="table" aria-label="Selected-system term pairs">
              <div className="cell-pair-row is-head system-term-pair-row" role="row">
                <span role="columnheader">System</span>
                <span role="columnheader">Term</span>
                <span role="columnheader">{detail.classX?.pharmClassCode ?? selectedCell.rowClassCode}</span>
                <span role="columnheader">{detail.classY?.pharmClassCode ?? selectedCell.columnClassCode}</span>
              </div>
              {detail.termPairs.map((pair) => (
                <div className="cell-pair-row system-term-pair-row" key={pair.id} role="row">
                  <span role="cell" title={pair.systemOrganClass}>{pair.systemOrganClass}</span>
                  <span role="cell" title={pair.parameterName}>{pair.parameterName}</span>
                  <span role="cell">
                    RR {formatDecimal(pair.rrX, 2)}
                    <small>
                      Log {formatDecimal(pair.logRrX, 2)} | {pair.precisionX} | {pair.significanceX}
                      {' | '} {formatCountLabel(pair.drugCountX, 'drug')}
                    </small>
                  </span>
                  <span role="cell">
                    RR {formatDecimal(pair.rrY, 2)}
                    <small>
                      Log {formatDecimal(pair.logRrY, 2)} | {pair.precisionY} | {pair.significanceY}
                      {' | '} {formatCountLabel(pair.drugCountY, 'drug')}
                    </small>
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
