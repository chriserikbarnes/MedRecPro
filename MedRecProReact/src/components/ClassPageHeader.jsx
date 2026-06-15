import { formatInteger } from '../lib/formatters';
import { ClassKpiStrip } from './ClassKpiStrip';

/**************************************************************/
/**
 * Renders one class metadata badge.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Metadata badge.
 */
function ClassBadge({ label, value = '', isOn = true }) {
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
 * Header for class-focused AE dashboard views.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Class header UI.
 */
export function ClassPageHeader({ selectedClass, picker, map, filters }) {
  return (
    <header className="page-header class-page-header">
      <div className="crumbs">
        <span>Inventory</span>
        <span>/</span>
        <span>By class</span>
        <span>/</span>
        <span>Adverse events</span>
      </div>

      <div className="drug-selector class-selector">
        {picker}

        <div className="coverage-row" aria-label="Class coverage">
          <ClassBadge
            label="Drugs"
            value={selectedClass ? formatInteger(map?.drugCount ?? selectedClass.drugCount) : ''}
            isOn={Boolean(selectedClass && (map?.drugCount ?? selectedClass.drugCount) > 0)}
          />
          <ClassBadge
            label="SOCs"
            value={selectedClass ? formatInteger(map?.soc?.length ?? selectedClass.socCount) : ''}
            isOn={Boolean(selectedClass && (map?.soc?.length ?? selectedClass.socCount) > 0)}
          />
          <ClassBadge
            label={selectedClass?.isCorrelatable ? 'Correlatable' : 'Too small'}
            isOn={Boolean(selectedClass?.isCorrelatable)}
          />
        </div>
      </div>

      <ClassKpiStrip selectedClass={selectedClass} map={map} filters={filters} />
    </header>
  );
}
