import {
  DEFAULT_PAGE_SIZE_OPTIONS,
  formatPageLabel,
  isPageDirectionDisabled,
  normalizePageSize,
} from './correlationPagerHelpers';

/**************************************************************/
/**
 * Compact correlation pager with previous/next controls and page-size options.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element | null} Pager UI.
 */
export function CorrelationPager({
  label,
  page,
  itemLabel = 'items',
  pageSizeOptions = DEFAULT_PAGE_SIZE_OPTIONS,
  onChangePage,
  onChangePageSize,
  isDisabled = false,
}) {
  if (!page) {
    return null;
  }

  return (
    <div className="corr-pager" aria-label={`${label} paging`}>
      <span className="corr-pager-title">{label}</span>
      <button
        type="button"
        className="corr-pager-button"
        aria-label={`Previous ${label} page`}
        disabled={isDisabled || isPageDirectionDisabled(page, 'previous')}
        onClick={() => onChangePage(Math.max(1, page.pageNumber - 1))}
      >
        <svg aria-hidden="true" viewBox="0 0 24 24">
          <path d="m15 18-6-6 6-6" />
        </svg>
      </button>
      <span className="corr-pager-label">{formatPageLabel(page, itemLabel)}</span>
      <button
        type="button"
        className="corr-pager-button"
        aria-label={`Next ${label} page`}
        disabled={isDisabled || isPageDirectionDisabled(page, 'next')}
        onClick={() => onChangePage(page.pageNumber + 1)}
      >
        <svg aria-hidden="true" viewBox="0 0 24 24">
          <path d="m9 18 6-6-6-6" />
        </svg>
      </button>
      {onChangePageSize ? (
        <label className="corr-page-size">
          <span>Size</span>
          <select
            value={normalizePageSize(page.pageSize, pageSizeOptions)}
            disabled={isDisabled}
            onChange={(event) => onChangePageSize(normalizePageSize(event.target.value, pageSizeOptions))}
          >
            {pageSizeOptions.map((option) => (
              <option key={option} value={option}>{option}</option>
            ))}
          </select>
        </label>
      ) : null}
    </div>
  );
}
