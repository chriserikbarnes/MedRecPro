import {
  formatComparatorCoverage,
  formatDecimal,
  formatDenominators,
  formatInteger,
} from '../lib/formatters';

/**************************************************************/
/**
 * Builds the score bar segment data for the chart-worthiness card.
 *
 * @param {number | null | undefined} score - Product score.
 * @returns {boolean[]} Segment fill flags.
 */
function buildScoreSegments(score) {
  // The prototype score bar uses ten evenly sized segments.
  const segmentCount = 10;

  // Missing scores render as an empty bar.
  const numericScore = Number.isFinite(Number(score)) ? Number(score) : 0;

  // The filled segment count is clamped so unusual scores do not break layout.
  const filledSegments = Math.max(0, Math.min(segmentCount, Math.round((numericScore / 100) * segmentCount)));

  // Each boolean drives one stable segment element.
  return Array.from({ length: segmentCount }, (_, index) => index < filledSegments);
}

/**************************************************************/
/**
 * Renders dashboard KPI cards from the selected product summary.
 *
 * @param {{ product: object | null }} props - Component props.
 * @returns {JSX.Element | null} KPI strip or null.
 */
export function KpiStrip({ product }) {
  // The strip waits for a selected product before rendering.
  if (!product) {
    return null;
  }

  // Elevated and protective counts are kept separate like the prototype card.
  const elevatedCount = product.significantElevated ?? Math.max(0, product.significant - product.significantProtective);

  // The score segments are calculated once for the rendered score.
  const scoreSegments = buildScoreSegments(product.score);

  return (
    <section className="kpi-strip" aria-label="Product adverse-event metrics">
      <article className="kpi-card">
        <div className="kpi-label">AE rows</div>
        <div className="kpi-value">{formatInteger(product.rowCount)}</div>
        <div className="kpi-sub">
          {formatDenominators(product.armN, product.comparatorN)} treatment / comparator
        </div>
      </article>

      <article className="kpi-card">
        <div className="kpi-label">Significant signals</div>
        <div className="kpi-value">{formatInteger(product.significant)}</div>
        <div className="kpi-sub">
          <span className="kpi-pip orange">{formatInteger(elevatedCount)} elevated</span>
          <span className="kpi-pip teal">{formatInteger(product.significantProtective)} protective</span>
        </div>
      </article>

      <article className="kpi-card">
        <div className="kpi-label">Comparator mix</div>
        <div className="kpi-value">
          {formatComparatorCoverage(product)}
          <span className="kpi-unit"> strata</span>
        </div>
        <div className="kpi-sub">{product.monoComboMix || 'Mixed source rows'}</div>
      </article>

      <article className="kpi-card">
        <div className="kpi-label">Chart-worthiness</div>
        <div className="kpi-value">
          {formatDecimal(product.score, 0)}
          <span className="kpi-unit">/100</span>
        </div>
        <div className="score-bar" aria-label={`Score ${formatDecimal(product.score, 0)} out of 100`}>
          {scoreSegments.map((isFilled, index) => (
            <div key={index} className={`score-seg${isFilled ? ' on' : ''}`} />
          ))}
        </div>
      </article>
    </section>
  );
}
