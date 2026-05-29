/**************************************************************/
/**
 * Empty state used when an API call succeeds with no dashboard rows.
 *
 * @param {{ title: string, body?: string }} props - Component props.
 * @returns {JSX.Element} Empty-state UI.
 */
export function EmptyState({ title, body = '' }) {
  return (
    <div className="empty-state">
      <strong>{title}</strong>
      {body ? <span>{body}</span> : null}
    </div>
  );
}
