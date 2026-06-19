const CORRELATION_COMPARATORS = ['Placebo', 'Active', 'Both'];
const CORRELATION_METHODS = ['Spearman', 'Pearson'];
const CORRELATION_AGGREGATIONS = ['MedianLogRr', 'MeanLogRr'];
const OMITTED_CLASS_TYPES = new Set(['CHEMICAL/INGREDIENT', 'EXT']);

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
 * Renders optional system class-type chips in the primary filter row.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element | null} Class-type chip controls.
 */
function ClassTypeFilter({
  facets = [],
  selectedClassType = 'All',
  isDisabled = false,
  onChangeClassType = null,
}) {
  if (!onChangeClassType) {
    return null;
  }

  const options = Array.isArray(facets) && facets.length > 0
    ? facets
    : [{ classType: 'All', displayLabel: 'All', classCount: 0, hasRenderableMap: false }];
  const visibleOptions = options.filter((facet) => !OMITTED_CLASS_TYPES.has(facet.classType));
  const shouldAppendSelected = selectedClassType
    && !OMITTED_CLASS_TYPES.has(selectedClassType)
    && !visibleOptions.some((facet) => facet.classType === selectedClassType);
  const selectedOptions = shouldAppendSelected
    ? [
      ...visibleOptions,
      {
        classType: selectedClassType,
        displayLabel: selectedClassType,
        classCount: 0,
        hasRenderableMap: false,
      },
    ]
    : visibleOptions;
  const activeClassType = OMITTED_CLASS_TYPES.has(selectedClassType) ? 'All' : selectedClassType;

  return (
    <>
      <span className="filter-label">TYPE</span>
      {selectedOptions.map((facet) => {
        const isSelected = facet.classType === activeClassType;
        const countLabel = `${facet.classCount ?? 0} classes`;

        return (
          <button
            key={facet.classType}
            type="button"
            className={`chip system-class-type-chip${isSelected ? ' active' : ''}`}
            aria-pressed={isSelected}
            aria-label={`${facet.displayLabel} class type, ${countLabel}`}
            title={`${facet.displayLabel} - ${countLabel}`}
            disabled={isDisabled}
            onClick={() => {
              if (!isSelected) {
                onChangeClassType(facet.classType);
              }
            }}
          >
            {facet.displayLabel}
          </button>
        );
      })}
    </>
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
  classTypeFacets = null,
  selectedClassType = 'All',
  isClassTypeDisabled = false,
  onChangeClassType = null,
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

        <ClassTypeFilter
          facets={classTypeFacets}
          selectedClassType={selectedClassType}
          isDisabled={isClassTypeDisabled}
          onChangeClassType={onChangeClassType}
        />
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
