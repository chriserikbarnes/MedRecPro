// Local recents are intentionally client-only and keyed by DocumentGUID.
const recentsStorageKey = 'medrecpro.aeDashboard.recents';

// Keep the picker concise and predictable.
const maxRecentProducts = 6;

/**************************************************************/
/**
 * Safely reads JSON from localStorage.
 *
 * @param {string} key - Storage key.
 * @param {unknown} fallback - Fallback value.
 * @returns {unknown} Parsed value or fallback.
 */
function readJsonStorage(key, fallback) {
  try {
    // localStorage can be unavailable in private browsing or locked-down hosts.
    const rawValue = globalThis.window?.localStorage?.getItem(key);

    // Missing values use the fallback without parsing.
    if (!rawValue) {
      return fallback;
    }

    return JSON.parse(rawValue);
  } catch {
    // Malformed or blocked storage should never break the dashboard.
    return fallback;
  }
}

/**************************************************************/
/**
 * Safely writes JSON to localStorage.
 *
 * @param {string} key - Storage key.
 * @param {unknown} value - Value to store.
 */
function writeJsonStorage(key, value) {
  try {
    // Storage writes are best-effort and never required for dashboard correctness.
    globalThis.window?.localStorage?.setItem(key, JSON.stringify(value));
  } catch {
    // Quota and privacy failures are ignored because recents are optional.
  }
}

/**************************************************************/
/**
 * Creates the display snapshot stored for a recent product.
 *
 * @param {object} product - Product view model.
 * @returns {object} Persistable recent snapshot.
 */
function createRecentSnapshot(product) {
  return {
    documentGuid: product.documentGuid,
    name: product.name,
    generic: product.generic,
    pharmClass: product.pharmClass,
    score: product.score,
  };
}

/**************************************************************/
/**
 * Reads recent products from localStorage.
 *
 * @returns {object[]} Recent product snapshots.
 */
export function getRecentProducts() {
  // Only arrays are accepted from storage.
  const storedValue = readJsonStorage(recentsStorageKey, []);

  // Invalid storage values are discarded.
  if (!Array.isArray(storedValue)) {
    return [];
  }

  return storedValue
    .filter((item) => item?.documentGuid)
    .slice(0, maxRecentProducts);
}

/**************************************************************/
/**
 * Saves a selected product as the most recent display snapshot.
 *
 * @param {object} product - Product view model.
 * @returns {object[]} Updated recents.
 */
export function saveRecentProduct(product) {
  // Missing products cannot be written as a recent selection.
  if (!product?.documentGuid) {
    return getRecentProducts();
  }

  // The new snapshot replaces any older snapshot for the same DocumentGUID.
  const snapshot = createRecentSnapshot(product);

  // Existing recents are filtered by GUID before the new snapshot is prepended.
  const existingRecents = getRecentProducts().filter(
    (recent) => recent.documentGuid !== snapshot.documentGuid,
  );

  // The bounded list keeps localStorage small and picker sections stable.
  const updatedRecents = [snapshot, ...existingRecents].slice(0, maxRecentProducts);

  writeJsonStorage(recentsStorageKey, updatedRecents);

  return updatedRecents;
}
