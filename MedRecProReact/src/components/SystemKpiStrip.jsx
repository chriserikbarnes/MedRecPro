import { formatInteger } from '../lib/formatters';

/**************************************************************/
/**
 * Counts usable and total off-diagonal system map cells.
 *
 * @param {object | null} map - Normalized system correlation map.
 * @returns {{ usable: number, total: number }} Cell counts.
 */
function countUsableCells(map) {
  const cells = Array.isArray(map?.cells) ? map.cells : [];
  const offDiagonalCells = cells.filter((cell) => !cell.isDiagonal);

  return {
    usable: offDiagonalCells.filter((cell) => cell.coefficient !== null && !cell.insufficientN).length,
    total: offDiagonalCells.length,
  };
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
 * Builds the compact comparator/filter context label.
 *
 * @param {object | null} map - Normalized system correlation map.
 * @param {object} fallbackFilters - Current client filters.
 * @returns {{ label: string, body: string }} KPI copy.
 */
function getFilterContext(map, fallbackFilters) {
  const filters = map?.appliedFilters ?? fallbackFilters;
  const comparator = filters.comparator ?? fallbackFilters.comparator;
  const floor = filters.minTermsPerCell ?? fallbackFilters.minTermsPerCell;
  const method = filters.method ?? fallbackFilters.method;
  const aggregation = filters.aggregation === 'MeanLogRr' ? 'mean' : 'median';

  return {
    label: comparator,
    body: `${method}, ${aggregation}, floor ${formatInteger(floor)} terms per cell`,
  };
}

/**************************************************************/
/**
 * System-level KPI cards for the selected MedDRA system.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element | null} KPI strip or null.
 */
export function SystemKpiStrip({ selectedSystems, map, filters }) {
  if (!Array.isArray(selectedSystems) || selectedSystems.length === 0) {
    return null;
  }

  const usableCells = countUsableCells(map);
  const filterContext = getFilterContext(map, filters);
  const classCount = map?.classCount ?? sumSystemField(selectedSystems, 'classCount');
  const drugCount = sumSystemField(selectedSystems, 'drugCount');
  const termCount = sumSystemField(selectedSystems, 'termCount');

  return (
    <section className="kpi-strip class-kpi-strip system-kpi-strip" aria-label="System adverse-event metrics">
      <article className="kpi-card">
        <div className="kpi-label">Classes with data</div>
        <div className="kpi-value">{formatInteger(classCount)}</div>
        <div className="kpi-sub">Selected MedDRA system</div>
      </article>

      <article className="kpi-card">
        <div className="kpi-label">Drugs represented</div>
        <div className="kpi-value">{formatInteger(drugCount)}</div>
        <div className="kpi-sub">Picker context before active paging</div>
      </article>

      <article className="kpi-card">
        <div className="kpi-label">Terms with data</div>
        <div className="kpi-value">{formatInteger(termCount)}</div>
        <div className="kpi-sub">Within selected MedDRA system</div>
      </article>

      <article className="kpi-card">
        <div className="kpi-label">Usable map cells</div>
        <div className="kpi-value">
          {formatInteger(usableCells.usable)}
          <span className="kpi-unit">/{formatInteger(usableCells.total)}</span>
        </div>
        <div className="kpi-sub">{filterContext.label} - {filterContext.body}</div>
      </article>
    </section>
  );
}
