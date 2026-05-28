/**************************************************************/
/**
 * Local storage helpers for AE dashboard recents.
 */
/**************************************************************/

const RECENTS_KEY = 'medrecpro:ae-dashboard:recents:v1';
const MAX_RECENTS = 12;

/**************************************************************/
/**
 * Loads recent product snapshots from localStorage.
 *
 * @returns {Array<Object>} Recent product snapshots.
 */
/**************************************************************/
export function loadRecents() {
    try {
        const value = JSON.parse(localStorage.getItem(RECENTS_KEY) || '[]');
        return Array.isArray(value) ? value.filter(item => item && item.documentGuid) : [];
    } catch (error) {
        return [];
    }
}

/**************************************************************/
/**
 * Persists recent product snapshots.
 *
 * @param {Array<Object>} recents Recent product snapshots.
 */
/**************************************************************/
export function saveRecents(recents) {
    try {
        localStorage.setItem(RECENTS_KEY, JSON.stringify(recents.slice(0, MAX_RECENTS)));
    } catch (error) {
        // Storage failure should not block dashboard use.
    }
}

/**************************************************************/
/**
 * Adds a product to the recent list using display-only snapshot fields.
 *
 * @param {Array<Object>} recents Existing recents.
 * @param {Object} product Selected product.
 * @returns {Array<Object>} Updated recents.
 */
/**************************************************************/
export function pushRecent(recents, product) {
    if (!product?.documentGuid) {
        return recents;
    }

    const snapshot = {
        documentGuid: product.documentGuid,
        name: product.name,
        generic: product.generic,
        pharmClass: product.pharmClass,
        score: product.score
    };

    const next = [
        snapshot,
        ...recents.filter(item => item.documentGuid !== product.documentGuid)
    ].slice(0, MAX_RECENTS);

    saveRecents(next);
    return next;
}
