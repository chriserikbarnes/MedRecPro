import { EmptyState } from '../common/EmptyState';
import { InlineError } from '../common/InlineError';
import { Loading } from '../common/Loading';
import { CorrelationCellDetail } from './CorrelationCellDetail';
import { CorrelationHeatmap } from './CorrelationHeatmap';
import { CorrelationMap } from './CorrelationMap';

const CLASS_VIEWS = ['map', 'heatmap'];
const CLASS_COMPARATORS = ['Placebo', 'Active', 'Both'];
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
 * Renders the prototype-style view selector for the class panel.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} View toggle.
 */
function ClassViewToggle({ classView, onChangeView }) {
  return (
    <div className="tabs class-view-tabs" role="tablist" aria-label="Class correlation views">
      {CLASS_VIEWS.map((view) => (
        <button
          type="button"
          key={view}
          className={`tab${classView === view ? ' active' : ''}`}
          role="tab"
          aria-selected={classView === view}
          onClick={() => onChangeView(view)}
        >
          {view === 'map' ? 'Correlation map' : 'Heatmap'}
        </button>
      ))}
    </div>
  );
}

/**************************************************************/
/**
 * Class filter controls shared by map and heatmap.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Filter UI.
 */
function ClassFilterControls({ filters, onChangeFilter }) {
  return (
    <>
      <div className="filter-row class-filter-row primary">
        <span className="filter-label">Comparator</span>
        {CLASS_COMPARATORS.map((comparator) => (
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

        <label className="class-number-filter">
          <span>Min drugs</span>
          <input
            type="number"
            min="3"
            max="99"
            value={filters.minDrugsPerCell}
            onChange={(event) => {
              const nextValue = Math.max(3, Number(event.target.value) || 3);
              onChangeFilter('minDrugsPerCell', nextValue);
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

/**************************************************************/
/**
 * Class correlation panel with filters, map, heatmap, and cell detail.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Class surface.
 */
export function ClassCorrelationSurface({
  selectedClass,
  classView,
  filters,
  classMap,
  classHeatmap,
  classCellDetail,
  selectedCell,
  isClassMapLoading,
  classMapError,
  onRetryClassMap,
  isClassHeatmapLoading,
  classHeatmapError,
  onRetryClassHeatmap,
  isClassCellLoading,
  classCellError,
  onRetryClassCell,
  onChangeClassView,
  onChangeClassFilter,
  onSelectCell,
}) {
  const activeTitle = classView === 'map' ? 'SOC correlation map' : 'SOC by drug heatmap';
  const activeSub =
    classView === 'map'
      ? 'Pairwise class-level SOC coefficients with small-n suppression preserved.'
      : 'Sparse per-drug LogRR cells that remain useful when correlation is thin.';

  if (!selectedClass) {
    return (
      <section className="panel">
        <EmptyState title="Choose a pharmacologic class." />
      </section>
    );
  }

  return (
    <>
      <section className="panel class-surface" data-screen-label="AE class correlation view">
        <div className="panel-header">
          <div className="panel-heading">
            <div className="panel-title">{activeTitle}</div>
            <div className="panel-sub">{activeSub}</div>
          </div>
          <div className="panel-actions class-view-actions">
            <ClassViewToggle classView={classView} onChangeView={onChangeClassView} />
          </div>
        </div>

        <ClassFilterControls
          filters={filters}
          onChangeFilter={onChangeClassFilter}
        />

        {filters.comparator === 'Both' ? (
          <div className="corr-warning comparator-warning" role="note">
            <p>Both comparator mixes placebo and active-comparator estimands. Interpret coefficient direction cautiously.</p>
          </div>
        ) : null}

        {classView === 'map' ? (
          <>
            {isClassMapLoading ? <Loading label="Loading correlation map" /> : null}
            {classMapError ? <InlineError error={classMapError} onRetry={onRetryClassMap} /> : null}
            {!isClassMapLoading && !classMapError ? (
              <CorrelationMap map={classMap} selectedCell={selectedCell} onSelectCell={onSelectCell} />
            ) : null}
          </>
        ) : null}

        {classView === 'heatmap' ? (
          <>
            {isClassHeatmapLoading ? <Loading label="Loading heatmap" /> : null}
            {classHeatmapError ? <InlineError error={classHeatmapError} onRetry={onRetryClassHeatmap} /> : null}
            {!isClassHeatmapLoading && !classHeatmapError ? (
              <CorrelationHeatmap heatmap={classHeatmap} />
            ) : null}
          </>
        ) : null}
      </section>

      {classView === 'map' ? (
        <CorrelationCellDetail
          selectedCell={selectedCell}
          detail={classCellDetail}
          isLoading={isClassCellLoading}
          error={classCellError}
          onRetry={onRetryClassCell}
        />
      ) : null}
    </>
  );
}
