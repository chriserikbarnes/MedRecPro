import { EmptyState } from '../common/EmptyState';
import { InlineError } from '../common/InlineError';
import { Loading } from '../common/Loading';
import { CorrelationFilterControls } from './CorrelationFilterControls';
import { CorrelationPager } from './CorrelationPager';
import { SystemCorrelationCellDetail } from './SystemCorrelationCellDetail';
import { SystemCorrelationHeatmap } from './SystemCorrelationHeatmap';
import { SystemCorrelationMap } from './SystemCorrelationMap';

const SYSTEM_VIEWS = ['map', 'heatmap'];

/**************************************************************/
/**
 * Renders the prototype-style view selector for the system panel.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} View toggle.
 */
function SystemViewToggle({ systemView, onChangeView }) {
  return (
    <div className="tabs class-view-tabs" role="tablist" aria-label="System correlation views">
      {SYSTEM_VIEWS.map((view) => (
        <button
          type="button"
          key={view}
          className={`tab${systemView === view ? ' active' : ''}`}
          role="tab"
          aria-selected={systemView === view}
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
 * System correlation panel with filters, paged map, heatmap, and term detail.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} System surface.
 */
export function SystemCorrelationSurface({
  selectedSystems,
  systemView,
  filters,
  systemMap,
  systemHeatmap,
  systemCellDetail,
  selectedCell,
  isSystemMapLoading,
  systemMapError,
  onRetrySystemMap,
  isSystemHeatmapLoading,
  systemHeatmapError,
  onRetrySystemHeatmap,
  isSystemCellLoading,
  systemCellError,
  onRetrySystemCell,
  onChangeSystemView,
  onChangeSystemFilter,
  onSelectCell,
  mapClassPage,
  heatmapClassPage,
  heatmapDrugPage,
  termPairPage,
  systemClassType,
  includeFullMatrix,
  onChangeClassType,
  onToggleFullMatrix,
  onChangeMapClassPage,
  onChangeMapClassPageSize,
  onChangeHeatmapClassPage,
  onChangeHeatmapClassPageSize,
  onChangeHeatmapDrugPage,
  onChangeHeatmapDrugPageSize,
  onChangeTermPairPage,
  onChangeTermPairPageSize,
}) {
  const activeTitle = systemView === 'map' ? 'Class correlation map' : 'Class by drug heatmap';
  const activeSub =
    systemView === 'map'
      ? 'Pairwise pharmacologic-class coefficients over selected MedDRA system terms.'
      : 'Sparse per-drug selected-system LogRR cells across pharmacologic classes.';
  const classTypeFacets = systemView === 'map'
    ? systemMap?.classTypeFacets
    : systemHeatmap?.classTypeFacets;
  const isClassTypeDisabled = systemView === 'map'
    ? isSystemMapLoading
    : isSystemHeatmapLoading;

  if (!Array.isArray(selectedSystems) || selectedSystems.length === 0) {
    return (
      <section className="panel">
        <EmptyState title="Choose a MedDRA system." />
      </section>
    );
  }

  return (
    <>
      <section className="panel class-surface system-surface" data-screen-label="AE system correlation view">
        <div className="panel-header">
          <div className="panel-heading">
            <div className="panel-title">{activeTitle}</div>
            <div className="panel-sub">{activeSub}</div>
          </div>
          <div className="panel-actions class-view-actions">
            <SystemViewToggle systemView={systemView} onChangeView={onChangeSystemView} />
          </div>
        </div>

        <CorrelationFilterControls
          filters={filters}
          onChangeFilter={onChangeSystemFilter}
          floorKey="minTermsPerCell"
          floorLabel="Min terms"
          classTypeFacets={classTypeFacets}
          selectedClassType={systemClassType}
          isClassTypeDisabled={isClassTypeDisabled}
          onChangeClassType={onChangeClassType}
          afterExcludeCombosControl={systemView === 'map' ? (
            <button
              type="button"
              className={`chip-toggle${includeFullMatrix ? ' on' : ''}`}
              onClick={onToggleFullMatrix}
            >
              <span className="sw" aria-hidden="true" />
              Full matrix
            </button>
          ) : null}
        />

        {filters.comparator === 'Both' ? (
          <div className="corr-warning comparator-warning" role="note">
            <p>Both comparator mixes placebo and active-comparator estimands. Interpret coefficient direction cautiously.</p>
          </div>
        ) : null}

        {systemView === 'map' ? (
          <>
            <div className="corr-control-row">
              {includeFullMatrix ? (
                <span className="corr-control-note">
                  {systemMap?.includesFullMatrix ? 'Full matrix returned' : 'Full matrix requested'}
                </span>
              ) : (
                <CorrelationPager
                  label="Classes"
                  page={systemMap?.classPage ?? mapClassPage}
                  itemLabel="classes"
                  pageSizeOptions={[10, 20, 40, 80, 100]}
                  onChangePage={onChangeMapClassPage}
                  onChangePageSize={onChangeMapClassPageSize}
                  isDisabled={isSystemMapLoading}
                />
              )}
            </div>
            {isSystemMapLoading ? <Loading label="Loading system correlation map" /> : null}
            {systemMapError ? <InlineError error={systemMapError} onRetry={onRetrySystemMap} /> : null}
            {!isSystemMapLoading && !systemMapError ? (
              <SystemCorrelationMap map={systemMap} selectedCell={selectedCell} onSelectCell={onSelectCell} />
            ) : null}
          </>
        ) : null}

        {systemView === 'heatmap' ? (
          <>
            <div className="corr-control-row">
              <CorrelationPager
                label="Class"
                page={systemHeatmap?.classPage ?? heatmapClassPage}
                itemLabel="classes"
                pageSizeOptions={[10, 20, 40, 80, 100]}
                onChangePage={onChangeHeatmapClassPage}
                onChangePageSize={onChangeHeatmapClassPageSize}
                isDisabled={isSystemHeatmapLoading}
              />
              <CorrelationPager
                label="Drug"
                page={systemHeatmap?.drugPage ?? heatmapDrugPage}
                itemLabel="drugs"
                pageSizeOptions={[25, 50, 100, 200]}
                onChangePage={onChangeHeatmapDrugPage}
                onChangePageSize={onChangeHeatmapDrugPageSize}
                isDisabled={isSystemHeatmapLoading}
              />
            </div>
            {isSystemHeatmapLoading ? <Loading label="Loading system heatmap" /> : null}
            {systemHeatmapError ? <InlineError error={systemHeatmapError} onRetry={onRetrySystemHeatmap} /> : null}
            {!isSystemHeatmapLoading && !systemHeatmapError ? (
              <SystemCorrelationHeatmap heatmap={systemHeatmap} />
            ) : null}
          </>
        ) : null}
      </section>

      {systemView === 'map' ? (
        <SystemCorrelationCellDetail
          selectedCell={selectedCell}
          detail={systemCellDetail}
          isLoading={isSystemCellLoading}
          error={systemCellError}
          onRetry={onRetrySystemCell}
          termPairPage={termPairPage}
          onChangeTermPairPage={onChangeTermPairPage}
          onChangeTermPairPageSize={onChangeTermPairPageSize}
        />
      ) : null}
    </>
  );
}
