import { useCallback, useEffect, useMemo, useState } from 'react';
import { AdverseEventClient } from '../api/adverseEventClient';
import { normalizeProducts } from '../lib/normalizers';
import { useDebouncedValue } from './useDebouncedValue';

// The prototype picker displays the full dashboard-ready catalog count.
const productPageSize = 450;

/**************************************************************/
/**
 * Loads the product picker catalog from the live AdverseEvent API.
 *
 * @param {string} searchTerm - User-entered server search term.
 * @returns {object} Product loading state and mutation helpers.
 */
export function useProducts(searchTerm) {
  // Product rows currently visible in the picker.
  const [products, setProducts] = useState([]);

  // Loading tracks the active catalog request.
  const [isLoading, setIsLoading] = useState(true);

  // Error stores the most recent catalog request failure.
  const [error, setError] = useState(null);

  // Total distinct-product inventory shown in the picker count badge. This is the
  // real baseline from the API, independent of search text or paging.
  const [totalProductCount, setTotalProductCount] = useState(0);

  // Refresh tokens let retry buttons re-run the current request.
  const [refreshToken, setRefreshToken] = useState(0);

  // Search is debounced before it reaches the server.
  const debouncedSearchTerm = useDebouncedValue(searchTerm, 250);

  /**************************************************************/
  /**
   * Reruns the current catalog query.
   */
  const refresh = useCallback(() => {
    // Incrementing the token invalidates the current effect dependencies.
    setRefreshToken((currentToken) => currentToken + 1);
  }, []);

  /**************************************************************/
  /**
   * Updates one product in-place after favorite mutations or hydration.
   *
   * @param {object} updatedProduct - Product view model.
   */
  const updateProduct = useCallback((updatedProduct) => {
    // Products without document GUIDs cannot be matched safely.
    if (!updatedProduct?.documentGuid) {
      return;
    }

    setProducts((currentProducts) =>
      currentProducts.map((product) =>
        product.documentGuid === updatedProduct.documentGuid
          ? { ...product, ...updatedProduct }
          : product,
      ),
    );
  }, []);

  useEffect(() => {
    // AbortController prevents stale search responses from replacing newer ones.
    const requestController = new AbortController();

    // Keep a local flag because some browsers resolve aborted promises differently.
    let isCurrentRequest = true;

    /**************************************************************/
    /**
     * Runs the catalog request and commits only the current response.
     */
    async function loadProducts() {
      setIsLoading(true);
      setError(null);

      try {
        // The slim catalog endpoint is served from a shared cache and performs
        // product/substance/UNII/class/ingredient search server-side.
        const payload = await AdverseEventClient.getProductCatalog({
          productSearch: debouncedSearchTerm.trim(),
          pageNumber: 1,
          pageSize: productPageSize,
          signal: requestController.signal,
        });

        // Aborted or superseded responses are ignored.
        if (!isCurrentRequest) {
          return;
        }

        setProducts(normalizeProducts(payload));
      } catch (requestError) {
        // Abort is expected when search text changes quickly.
        if (requestError.name === 'AbortError') {
          return;
        }

        // Superseded failures should not overwrite the current request state.
        if (!isCurrentRequest) {
          return;
        }

        setError(requestError);
      } finally {
        // Only the current request owns the loading flag.
        if (isCurrentRequest) {
          setIsLoading(false);
        }
      }
    }

    loadProducts();

    // Cleanup marks the request stale and aborts the in-flight fetch.
    return () => {
      isCurrentRequest = false;
      requestController.abort();
    };
  }, [debouncedSearchTerm, refreshToken]);

  useEffect(() => {
    // The inventory count is search-independent, so it loads once per mount and
    // again only when a retry bumps the refresh token.
    const requestController = new AbortController();

    // Stale-response guard mirrors the catalog effect above.
    let isCurrentRequest = true;

    /**************************************************************/
    /**
     * Loads the real distinct-product inventory count for the badge.
     */
    async function loadProductCount() {
      try {
        const count = await AdverseEventClient.getProductCount({
          signal: requestController.signal,
        });

        // Superseded or aborted responses are ignored.
        if (!isCurrentRequest) {
          return;
        }

        // The endpoint returns a bare integer; coerce defensively.
        setTotalProductCount(Number(count) || 0);
      } catch (requestError) {
        // Abort is expected during unmount; a failed count simply leaves the
        // badge at its previous value rather than surfacing a blocking error.
        if (requestError.name === 'AbortError') {
          return;
        }
      }
    }

    loadProductCount();

    // Cleanup cancels the in-flight count request.
    return () => {
      isCurrentRequest = false;
      requestController.abort();
    };
  }, [refreshToken]);

  // A memoized lookup avoids repeated linear scans in selection logic.
  const productsByGuid = useMemo(() => {
    // Map keys are lowercase GUIDs so casing differences never matter.
    const lookup = new Map();

    // Each loaded product contributes one lookup entry.
    for (const product of products) {
      lookup.set(product.documentGuid.toLowerCase(), product);
    }

    return lookup;
  }, [products]);

  return {
    products,
    productsByGuid,
    totalProductCount,
    isLoading,
    error,
    refresh,
    updateProduct,
  };
}
