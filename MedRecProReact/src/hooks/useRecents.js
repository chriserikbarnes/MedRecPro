import { useCallback, useState } from 'react';
import { getRecentProducts, saveRecentProduct } from '../lib/storage';

/**************************************************************/
/**
 * Manages client-local recent products.
 *
 * @returns {object} Recent product state and mutation helper.
 */
export function useRecents() {
  // Initial recents are read lazily from localStorage.
  const [recentProducts, setRecentProducts] = useState(() => getRecentProducts());

  /**************************************************************/
  /**
   * Records a selected product as the latest recent.
   *
   * @param {object} product - Product view model.
   */
  const recordRecentProduct = useCallback((product) => {
    // Storage helper returns the updated bounded list.
    const updatedRecents = saveRecentProduct(product);

    setRecentProducts(updatedRecents);
  }, []);

  return {
    recentProducts,
    recordRecentProduct,
  };
}
