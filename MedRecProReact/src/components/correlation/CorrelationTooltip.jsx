/**************************************************************/
/**
 * Compact hover/focus tooltip for correlation cells.
 *
 * @param {{ children: React.ReactNode }} props - Component props.
 * @returns {JSX.Element} Tooltip.
 */
export function CorrelationTooltip({ children }) {
  return (
    <span className="corr-tooltip" role="tooltip">
      {children}
    </span>
  );
}
