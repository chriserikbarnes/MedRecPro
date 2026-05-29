import { useEffect, useState } from 'react';

/**************************************************************/
/**
 * Delays value updates so server-side product search does not fire per keypress.
 *
 * @param {unknown} value - Current input value.
 * @param {number} delayMilliseconds - Debounce duration.
 * @returns {unknown} Debounced value.
 */
export function useDebouncedValue(value, delayMilliseconds) {
  // The debounced state starts aligned with the immediate value.
  const [debouncedValue, setDebouncedValue] = useState(value);

  useEffect(() => {
    // The timer commits the latest value after the quiet period.
    const debounceTimer = window.setTimeout(() => {
      setDebouncedValue(value);
    }, delayMilliseconds);

    // Cleanup cancels stale timers when the user keeps typing.
    return () => {
      window.clearTimeout(debounceTimer);
    };
  }, [value, delayMilliseconds]);

  return debouncedValue;
}
