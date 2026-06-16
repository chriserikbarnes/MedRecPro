/**************************************************************/
/**
 * Renders the product/class focus segmented control.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Focus switch.
 */
export function FocusSwitch({ activeFocus, onChangeFocus }) {
  return (
    <div className="focus-strip" aria-label="Dashboard focus">
      <div className="focus-strip-inner">
        <div className="focus-switch" role="tablist" aria-label="Dashboard focus">
          <button
            type="button"
            className={`focus-option${activeFocus === 'product' ? ' active' : ''}`}
            role="tab"
            aria-selected={activeFocus === 'product'}
            onClick={() => onChangeFocus('product')}
          >
            <svg aria-hidden="true" viewBox="0 0 24 24" className="focus-icon">
              <path d="M10 13a5 5 0 0 0 7.5.5l2-2a5 5 0 0 0-7-7l-1.1 1.1" />
              <path d="M14 11a5 5 0 0 0-7.5-.5l-2 2a5 5 0 0 0 7 7l1.1-1.1" />
            </svg>
            <span>Per-product</span>
          </button>
          <button
            type="button"
            className={`focus-option${activeFocus === 'class' ? ' active' : ''}`}
            role="tab"
            aria-selected={activeFocus === 'class'}
            onClick={() => onChangeFocus('class')}
          >
            <svg aria-hidden="true" viewBox="0 0 24 24" className="focus-icon">
              <rect x="4" y="4" width="16" height="16" rx="1.5" />
              <path d="M4 10h16" />
              <path d="M4 16h16" />
              <path d="M10 4v16" />
              <path d="M16 4v16" />
            </svg>
            <span>By class</span>
          </button>
        </div>
        <span className="focus-strip-hint">One pharmacologic class {'->'} SOC correlation map & heatmap</span>
      </div>
    </div>
  );
}
