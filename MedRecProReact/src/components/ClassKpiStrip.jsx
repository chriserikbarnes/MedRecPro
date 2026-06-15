import { formatInteger } from '../lib/formatters';

/**************************************************************/
/**
 * Counts usable and total off-diagonal map cells.
 *
 * @param {object | null} map - Normalized correlation map.
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
 * Builds the compact comparator/filter context label.
 *
 * @param {object | null} map - Normalized correlation map.
 * @param {object} fallbackFilters - Current client filters.
 * @returns {{ label: string, body: string }} KPI copy.
 */
function getFilterContext(map, fallbackFilters) {
  const filters = map?.appliedFilters ?? fallbackFilters;
  const comparator = filters.comparator ?? fallbackFilters.comparator;
  const floor = filters.minDrugsPerCell ?? fallbackFilters.minDrugsPerCell;
  const method = filters.method ?? fallbackFilters.method;

  return {
    label: comparator,
    body: `${method}, floor ${formatInteger(floor)} drugs per cell`,
  };
}

/**************************************************************/
/**
 * Class-level KPI cards for the selected pharmacologic class.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element | null} KPI strip or null.
 */
export function ClassKpiStrip({ selectedClass, map, filters }) {
  if (!selectedClass) {
    return null;
  }

  const usableCells = countUsableCells(map);
  const filterContext = getFilterContext(map, filters);
  const drugCount = map?.drugCount ?? selectedClass.drugCount;
  const socCount = map?.soc?.length ?? selectedClass.socCount;

  return (
    <section className="kpi-strip class-kpi-strip" aria-label="Class adverse-event metrics">
      <article className="kpi-card">
        <div className="kpi-label">Drugs in class</div>
        <div className="kpi-value">{formatInteger(drugCount)}</div>
        <div className="kpi-sub">{selectedClass.pharmClassCode}</div>
      </article>

      <article className="kpi-card">
        <div className="kpi-label">SOCs with data</div>
        <div className="kpi-value">{formatInteger(socCount)}</div>
        <div className="kpi-sub">{formatInteger(selectedClass.socCount)} before active filters</div>
      </article>

      <article className="kpi-card">
        <div className="kpi-label">Usable map cells</div>
        <div className="kpi-value">
          {formatInteger(usableCells.usable)}
          <span className="kpi-unit">/{formatInteger(usableCells.total)}</span>
        </div>
        <div className="kpi-sub">Off-diagonal coefficients above the floor</div>
      </article>

      <article className="kpi-card">
        <div className="kpi-label">Comparator/filter</div>
        <div className="kpi-value">{filterContext.label}</div>
        <div className="kpi-sub">{filterContext.body}</div>
      </article>
    </section>
  );
}
