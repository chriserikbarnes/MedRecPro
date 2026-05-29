/**************************************************************/
/**
 * Compact loading state used by the dashboard shell and picker.
 *
 * @param {{ label?: string }} props - Component props.
 * @returns {JSX.Element} Loading UI.
 */
export function Loading({ label = 'Loading' }) {
  return (
    <div className="loading-state" role="status" aria-live="polite">
      <span className="loading-spinner" aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}
