/**************************************************************/
/**
 * Inline recoverable error with an optional retry action.
 *
 * @param {{ error: Error | string, onRetry?: () => void }} props - Component props.
 * @returns {JSX.Element} Error UI.
 */
export function InlineError({ error, onRetry = null }) {
  // Normalize error objects and strings into one message.
  const message = typeof error === 'string' ? error : error?.message;

  return (
    <div className="inline-error" role="alert">
      <span>{message || 'Something went wrong.'}</span>
      {onRetry ? (
        <button type="button" className="button button-secondary" onClick={onRetry}>
          Retry
        </button>
      ) : null}
    </div>
  );
}
