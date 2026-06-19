const CORRELATION_COMPARATORS = ['Placebo', 'Active', 'Both'];
const CORRELATION_METHODS = ['Spearman', 'Pearson'];
const CORRELATION_AGGREGATIONS = ['MedianLogRr', 'MeanLogRr'];

/**************************************************************/
/**
 * Formats an aggregation enum into compact UI text.
 *
 * @param {string} aggregation - Aggregation token.
 * @returns {string} Display label.
 */
function formatAggregation(aggregation) {
  return aggregation === 'MeanLogRr' ? 'Mean LogRR' : 'Median LogRR';
}

/**************************************************************/
/**
 * Renders a chip toggle with the shared switch glyph.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Toggle button.
 */
function FilterToggle({ isOn, onClick, children }) {
  return (
    <button type="button" className={`chip-toggle${isOn ? ' on' : ''}`} onClick={onClick}>
      <span className="sw" aria-hidden="true" />
      {children}
    </button>
  );
}

/**************************************************************/
/**
 * Shared class/system correlation filter controls.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Filter UI.
 */
export function CorrelationFilterControls({
  filters,
  onChangeFilter,
  floorKey,
  floorLabel,
  afterExcludeCombosControl = null,
}) {
  return (
    <>
      <div className="filter-row class-filter-row primary">
        <span className="filter-label">Comparator</span>
        {CORRELATION_COMPARATORS.map((comparator) => (
          <button
            type="button"
            key={comparator}
            className={`chip${filters.comparator === comparator ? ' active' : ''}`}
            onClick={() => onChangeFilter('comparator', comparator)}
          >
            {comparator}
          </button>
        ))}

        <span className="filter-label">Method</span>
        {CORRELATION_METHODS.map((method) => (
          <button
            type="button"
            key={method}
            className={`chip${filters.method === method ? ' active' : ''}`}
            onClick={() => onChangeFilter('method', method)}
          >
            {method}
          </button>
        ))}

        <span className="filter-label">Aggregation</span>
        {CORRELATION_AGGREGATIONS.map((aggregation) => (
          <button
            type="button"
            key={aggregation}
            className={`chip${filters.aggregation === aggregation ? ' active' : ''}`}
            onClick={() => onChangeFilter('aggregation', aggregation)}
          >
            {formatAggregation(aggregation)}
          </button>
        ))}
      </div>

      <div className="filter-row class-filter-row advanced">
        <FilterToggle
          isOn={filters.includeNonSignificant}
          onClick={() => onChangeFilter('includeNonSignificant', !filters.includeNonSignificant)}
        >
          Include non-significant
        </FilterToggle>
        <FilterToggle
          isOn={filters.excludeFragile}
          onClick={() => onChangeFilter('excludeFragile', !filters.excludeFragile)}
        >
          Exclude fragile
        </FilterToggle>
        <FilterToggle
          isOn={filters.excludeCombos}
          onClick={() => onChangeFilter('excludeCombos', !filters.excludeCombos)}
        >
          Exclude combos
        </FilterToggle>
        {afterExcludeCombosControl}

        <label className="class-number-filter">
          <span>{floorLabel}</span>
          <input
            type="number"
            min="3"
            max="99"
            value={filters[floorKey]}
            onChange={(event) => {
              const nextValue = Math.max(3, Number(event.target.value) || 3);
              onChangeFilter(floorKey, nextValue);
            }}
          />
        </label>

        <label className="class-number-filter">
          <span>Min events</span>
          <input
            type="number"
            min="0"
            max="9999"
            value={filters.minEvents}
            onChange={(event) => {
              const nextValue = Math.max(0, Number(event.target.value) || 0);
              onChangeFilter('minEvents', nextValue);
            }}
          />
        </label>
      </div>
    </>
  );
}
