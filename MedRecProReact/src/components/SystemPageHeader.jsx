import { formatInteger } from '../lib/formatters';
import { SystemKpiStrip } from './SystemKpiStrip';

/**************************************************************/
/**
 * Renders one system metadata badge.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Metadata badge.
 */
function SystemBadge({ label, value = '', isOn = true }) {
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
 * Sums a numeric field across selected picker rows.
 *
 * @param {object[]} selectedSystems - Selected system rows.
 * @param {string} key - Numeric field name.
 * @returns {number} Summed value.
 */
function sumSystemField(selectedSystems, key) {
  return selectedSystems.reduce((total, system) => total + (Number(system?.[key]) || 0), 0);
}

/**************************************************************/
/**
 * Header for MedDRA-system-focused AE dashboard views.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} System header UI.
 */
export function SystemPageHeader({ selectedSystems, picker, map, filters }) {
  const hasSelection = Array.isArray(selectedSystems) && selectedSystems.length > 0;
  const classCount = hasSelection ? (map?.classCount ?? sumSystemField(selectedSystems, 'classCount')) : 0;
  const drugCount = hasSelection ? sumSystemField(selectedSystems, 'drugCount') : 0;
  const termCount = hasSelection ? sumSystemField(selectedSystems, 'termCount') : 0;
  const selectedSystem = hasSelection ? selectedSystems[0] : null;
  const hasMapReadySystem = Boolean(selectedSystem?.hasRenderableMap);

  return (
    <header className="page-header class-page-header system-page-header">
      <div className="crumbs">
        <span>Inventory</span>
        <span>/</span>
        <span>By system</span>
        <span>/</span>
        <span>Adverse events</span>
      </div>

      <div className="drug-selector class-selector system-selector">
        {picker}

        <div className="coverage-row" aria-label="System coverage">
          <SystemBadge
            label="Classes"
            value={hasSelection ? formatInteger(classCount) : ''}
            isOn={classCount > 0}
          />
          <SystemBadge
            label="Drugs"
            value={hasSelection ? formatInteger(drugCount) : ''}
            isOn={drugCount > 0}
          />
          <SystemBadge
            label="Terms"
            value={hasSelection ? formatInteger(termCount) : ''}
            isOn={termCount > 0}
          />
          <SystemBadge
            label={hasMapReadySystem ? 'Map ready' : 'Heatmap only'}
            isOn={hasMapReadySystem}
          />
        </div>
      </div>

      <SystemKpiStrip selectedSystems={selectedSystems} map={map} filters={filters} />
    </header>
  );
}
