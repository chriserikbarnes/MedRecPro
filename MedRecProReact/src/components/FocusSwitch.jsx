/**************************************************************/
/**
 * Renders the product/class focus segmented control.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Focus switch.
 */
export function FocusSwitch({ activeFocus, onChangeFocus }) {
  return (
    <div className="focus-switch-row" aria-label="Dashboard focus">
      <div className="focus-switch" role="tablist" aria-label="Dashboard focus">
        <button
          type="button"
          className={`focus-option${activeFocus === 'product' ? ' active' : ''}`}
          role="tab"
          aria-selected={activeFocus === 'product'}
          onClick={() => onChangeFocus('product')}
        >
          <svg aria-hidden="true" viewBox="0 0 24 24" className="focus-icon">
            <path d="M8 3h8l2 4v12a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2V7l2-4Z" />
            <path d="M8 7h8" />
            <path d="M10 12h4" />
          </svg>
          <span>Product</span>
        </button>
        <button
          type="button"
          className={`focus-option${activeFocus === 'class' ? ' active' : ''}`}
          role="tab"
          aria-selected={activeFocus === 'class'}
          onClick={() => onChangeFocus('class')}
        >
          <svg aria-hidden="true" viewBox="0 0 24 24" className="focus-icon">
            <path d="M5 7h14" />
            <path d="M7 7v10" />
            <path d="M17 7v10" />
            <path d="M4 17h16" />
            <path d="M9.5 11h5" />
          </svg>
          <span>Class</span>
        </button>
      </div>
    </div>
  );
}
