import { formatInteger, formatPercent } from '../lib/formatters';
import { KpiStrip } from './KpiStrip';

/**************************************************************/
/**
 * Renders one coverage badge for the selected product header.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Coverage badge.
 */
function CoverageBadge({ label, value, isOn }) {
  return (
    <span className={`cov-badge${isOn ? ' is-on' : ' is-off'}`}>
      <span className="cov-dot" aria-hidden="true" />
      <span>{label}</span>
      {value ? <span className="cov-num">{value}</span> : null}
    </span>
  );
}

/**************************************************************/
/**
 * Renders the selected product identity, prototype picker, coverage row, and KPI strip.
 *
 * @param {{ product: object | null, picker: JSX.Element, hydrationError?: Error | null }} props - Component props.
 * @returns {JSX.Element} Header UI.
 */
export function PageHeader({ product, picker, hydrationError = null }) {
  // Dose coverage stays visible as a percentage even when no dose rows exist.
  const doseCoverage = product ? formatPercent(product.doseCoverage) : '';

  // SOC breadth mirrors the server aggregate fields.
  const socBreadth = product
    ? `${formatInteger(product.socBreadth)} / ${formatInteger(product.socTotal)}`
    : '';

  return (
    <header className="page-header">
      <div className="crumbs">
        <span>Inventory</span>
        <span>/</span>
        <span>Per-product view</span>
        <span>/</span>
        <span>Adverse events</span>
      </div>

      <div className="drug-selector">
        {picker}

        {hydrationError ? (
          <p className="header-warning">The linked product was not dashboard-ready. Choose another product.</p>
        ) : null}

        <div className="coverage-row" aria-label="Product coverage">
          <CoverageBadge label="Placebo-controlled" isOn={Boolean(product?.placeboCoverage)} />
          <CoverageBadge label={product?.activeCoverage ? 'Active comparator' : 'No active comparator'} isOn={Boolean(product?.activeCoverage)} />
          <CoverageBadge label="Dose data" value={doseCoverage} isOn={Boolean(product && product.doseCoverage > 0)} />
          <CoverageBadge label="SOC breadth" value={socBreadth} isOn={Boolean(product && product.socBreadth > 0)} />
        </div>
      </div>

      <KpiStrip product={product} />
    </header>
  );
}
