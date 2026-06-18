import { EmptyState } from '../common/EmptyState';
import { InlineError } from '../common/InlineError';
import { Loading } from '../common/Loading';
import { CorrelationCellDetail } from './CorrelationCellDetail';
import { CorrelationFilterControls } from './CorrelationFilterControls';
import { CorrelationHeatmap } from './CorrelationHeatmap';
import { CorrelationMap } from './CorrelationMap';

const CLASS_VIEWS = ['map', 'heatmap'];

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

        <CorrelationFilterControls
          filters={filters}
          onChangeFilter={onChangeClassFilter}
          floorKey="minDrugsPerCell"
          floorLabel="Min drugs"
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
