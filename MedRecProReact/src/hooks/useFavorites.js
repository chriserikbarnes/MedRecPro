import { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError } from '../api/apiError';
import { AdverseEventClient } from '../api/adverseEventClient';
import { normalizeProducts } from '../lib/normalizers';

// Favorite hydration uses the same compact page size as the picker.
const favoritePageSize = 25;

/**************************************************************/
/**
 * Loads and mutates API-backed favorite products.
 *
 * @returns {object} Favorite state and action helpers.
 */
export function useFavorites() {
  // Favorite product summaries returned by the authenticated-only endpoint.
  const [favoriteProducts, setFavoriteProducts] = useState([]);

  // Notice is used for auth and permission prompts.
  const [favoriteNotice, setFavoriteNotice] = useState('');

  // Busy GUIDs keep individual favorite buttons stable during mutations.
  const [busyDocumentGuids, setBusyDocumentGuids] = useState(() => new Set());

  // Refresh tokens let callers rehydrate favorites after recoverable failures.
  const [refreshToken, setRefreshToken] = useState(0);

  /**************************************************************/
  /**
   * Requests a favorite-list reload.
   */
  const refreshFavorites = useCallback(() => {
    // Incrementing the token keeps the effect dependency simple.
    setRefreshToken((currentToken) => currentToken + 1);
  }, []);

  useEffect(() => {
    // AbortController prevents unmounted favorite loads from committing state.
    const requestController = new AbortController();

    // Local current flag mirrors the product hook's stale-response guard.
    let isCurrentRequest = true;

    /**************************************************************/
    /**
     * Loads favorites when the user has API access.
     */
    async function loadFavorites() {
      try {
        // Anonymous users receive 401; that simply means no favorite section.
        const payload = await AdverseEventClient.getFavorites({
          pageNumber: 1,
          pageSize: favoritePageSize,
          signal: requestController.signal,
        });

        // Superseded responses are ignored.
        if (!isCurrentRequest) {
          return;
        }

        setFavoriteProducts(normalizeProducts(payload));
        setFavoriteNotice('');
      } catch (requestError) {
        // Abort is expected during unmount or refresh.
        if (requestError.name === 'AbortError') {
          return;
        }

        // Superseded failures should not clear current favorite state.
        if (!isCurrentRequest) {
          return;
        }

        // 401/403 means favorites are unavailable for this caller.
        if (requestError instanceof ApiError && requestError.isAuthenticationOrPermissionFailure) {
          setFavoriteProducts([]);
          return;
        }

        setFavoriteNotice(requestError.message ?? 'Favorites are temporarily unavailable.');
      }
    }

    loadFavorites();

    // Cleanup cancels the load and prevents stale state writes.
    return () => {
      isCurrentRequest = false;
      requestController.abort();
    };
  }, [refreshToken]);

  /**************************************************************/
  /**
   * Toggles one product favorite through the live API.
   *
   * @param {object} product - Product view model.
   * @param {boolean} nextFavoriteState - Desired favorite state.
   * @returns {Promise<object | null>} Updated product or null when blocked.
   */
  const toggleFavorite = useCallback(async (product, nextFavoriteState) => {
    // Product rows without GUIDs cannot call the favorite endpoints.
    if (!product?.documentGuid) {
      return null;
    }

    // The busy set is copied to preserve React state immutability.
    setBusyDocumentGuids((currentBusySet) => {
      const nextBusySet = new Set(currentBusySet);
      nextBusySet.add(product.documentGuid);
      return nextBusySet;
    });

    try {
      // Favorite mutation choice follows the desired target state.
      if (nextFavoriteState) {
        await AdverseEventClient.addFavorite(product.documentGuid);
      } else {
        await AdverseEventClient.removeFavorite(product.documentGuid);
      }

      // The updated product carries the API-confirmed local favorite state.
      const updatedProduct = { ...product, isFavorite: nextFavoriteState };

      setFavoriteProducts((currentFavorites) => {
        // Removing a favorite filters it from the favorite section.
        if (!nextFavoriteState) {
          return currentFavorites.filter(
            (favorite) => favorite.documentGuid !== product.documentGuid,
          );
        }

        // Adding a favorite either updates an existing row or prepends it.
        const withoutCurrent = currentFavorites.filter(
          (favorite) => favorite.documentGuid !== product.documentGuid,
        );

        return [updatedProduct, ...withoutCurrent].slice(0, favoritePageSize);
      });

      setFavoriteNotice('');
      return updatedProduct;
    } catch (requestError) {
      // Auth and policy failures get a specific, non-retry prompt.
      if (requestError instanceof ApiError && requestError.isAuthenticationOrPermissionFailure) {
        setFavoriteNotice('Sign in with API access to save dashboard favorites.');
        return null;
      }

      setFavoriteNotice(requestError.message ?? 'Favorite update failed.');
      return null;
    } finally {
      // The busy set is copied again so React sees a new state reference.
      setBusyDocumentGuids((currentBusySet) => {
        const nextBusySet = new Set(currentBusySet);
        nextBusySet.delete(product.documentGuid);
        return nextBusySet;
      });
    }
  }, []);

  // Favorite lookups are lowercase to ignore GUID casing differences.
  const favoriteGuids = useMemo(() => {
    const lookup = new Set();

    // Each favorite product contributes one quick lookup value.
    for (const product of favoriteProducts) {
      lookup.add(product.documentGuid.toLowerCase());
    }

    return lookup;
  }, [favoriteProducts]);

  return {
    favoriteProducts,
    favoriteGuids,
    favoriteNotice,
    busyDocumentGuids,
    refreshFavorites,
    toggleFavorite,
  };
}
