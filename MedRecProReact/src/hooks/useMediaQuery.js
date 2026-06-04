import { useCallback, useSyncExternalStore } from 'react';

/**************************************************************/
/**
 * Tracks whether a CSS media query currently matches the viewport.
 *
 * @param {string} query - CSS media query string, e.g. '(max-width: 620px)'.
 * @returns {boolean} Whether the query currently matches.
 */
export function useMediaQuery(query) {
  // Subscribe binds the React store to matchMedia change events for this query.
  const subscribe = useCallback((onStoreChange) => {
    // matchMedia is unavailable during SSR/first paint, so there is nothing to watch.
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return () => {};
    }

    const mediaQueryList = window.matchMedia(query);
    mediaQueryList.addEventListener('change', onStoreChange);

    // Cleanup detaches the listener when the query changes or the component unmounts.
    return () => {
      mediaQueryList.removeEventListener('change', onStoreChange);
    };
  }, [query]);

  // The client snapshot reads the live match state on every render.
  const getSnapshot = () => (
    typeof window !== 'undefined' && typeof window.matchMedia === 'function'
      ? window.matchMedia(query).matches
      : false
  );

  // The server snapshot defaults to no match so SSR/first paint stays stable.
  const getServerSnapshot = () => false;

  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
