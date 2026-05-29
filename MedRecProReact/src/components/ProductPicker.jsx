import { useEffect, useMemo, useRef, useState } from 'react';
import { formatDecimal, formatFavoriteAction, formatInteger } from '../lib/formatters';
import { EmptyState } from './common/EmptyState';
import { InlineError } from './common/InlineError';
import { Loading } from './common/Loading';

/**************************************************************/
/**
 * Converts recents into product-like rows for picker display.
 *
 * @param {object[]} recents - Recent display snapshots.
 * @returns {object[]} Product-like recent rows.
 */
function mapRecentsToPickerRows(recents) {
  // Every recent snapshot already stores the client-safe document GUID.
  return recents.map((recent) => ({
    id: recent.documentGuid,
    documentGuid: recent.documentGuid,
    name: recent.name,
    generic: recent.generic,
    pharmClass: recent.pharmClass,
    score: recent.score,
    isRecentOnly: true,
  }));
}

/**************************************************************/
/**
 * Builds picker sections in display order.
 *
 * @param {object} args - Section input values.
 * @returns {object[]} Picker sections.
 */
function buildPickerSections({ favoriteProducts, recentProducts, products, searchTerm }) {
  // Favorite and recent sections are hidden during active server search.
  const showContextSections = searchTerm.trim().length === 0;

  // Duplicate rows are removed across sections by DocumentGUID.
  const usedDocumentGuids = new Set();

  // Section output is appended in the exact order it should render.
  const sections = [];

  /**************************************************************/
  /**
   * Adds a section after filtering duplicate or invalid rows.
   *
   * @param {string} id - Section identifier.
   * @param {string} label - Section label.
   * @param {object[]} rows - Candidate rows.
   */
  function addSection(id, label, rows) {
    // A section-local list preserves row order after duplicate filtering.
    const uniqueRows = [];

    // Each row is checked by normalized lowercase GUID.
    for (const row of rows) {
      // Invalid rows without GUIDs are skipped.
      if (!row?.documentGuid) {
        continue;
      }

      // Lowercase keys avoid casing mismatches between persisted recents and API data.
      const lookupKey = row.documentGuid.toLowerCase();

      // Duplicate rows already shown in higher-priority sections are skipped.
      if (usedDocumentGuids.has(lookupKey)) {
        continue;
      }

      usedDocumentGuids.add(lookupKey);
      uniqueRows.push(row);
    }

    // Empty sections are not rendered.
    if (uniqueRows.length > 0) {
      sections.push({ id, label, rows: uniqueRows });
    }
  }

  // Favorites come first when the user has any.
  if (showContextSections) {
    addSection('favorites', 'Favorites', favoriteProducts);
    addSection('recents', 'Recents', mapRecentsToPickerRows(recentProducts));
  }

  // Server results always render last so active searches feel predictable.
  addSection('results', searchTerm.trim() ? 'Search results' : 'Products', products);

  // Recents should name the visible count like the original prototype panel.
  for (const section of sections) {
    // Only the recents section gets dynamic label text.
    if (section.id === 'recents') {
      section.label = `Recent - last ${section.rows.length}`;
    }
  }

  return sections;
}

/**************************************************************/
/**
 * Renders the favorite star icon used by picker rows.
 *
 * @param {{ active?: boolean }} props - Component props.
 * @returns {JSX.Element} Icon SVG.
 */
function FavoriteIcon({ active = false }) {
  return (
    <svg
      aria-hidden="true"
      className="pi-star-icon"
      viewBox="0 0 24 24"
      fill={active ? 'currentColor' : 'none'}
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="2"
    >
      <polygon points="12 2 15 8.7 22 9.3 16.7 13.9 18.3 21 12 17.3 5.7 21 7.3 13.9 2 9.3 9 8.7 12 2" />
    </svg>
  );
}

/**************************************************************/
/**
 * Renders one picker row.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Picker row.
 */
function ProductPickerRow({
  product,
  isActive,
  isSelected,
  onSelect,
  onToggleFavorite,
  isFavoriteBusy,
}) {
  // Favorite action reflects the visible row state.
  const favoriteLabel = formatFavoriteAction(product.isFavorite);

  // Significant count defaults to zero for recent-only snapshots.
  const significantCount = formatInteger(product.significant ?? 0);

  return (
    <div
      id={`product-option-${product.documentGuid}`}
      className={`picker-item${isActive ? ' is-active' : ''}${isSelected ? ' is-selected' : ''}`}
      role="option"
      aria-selected={isSelected}
      onMouseDown={(event) => {
        // Prevent input blur before selection commits.
        event.preventDefault();
      }}
      onClick={() => onSelect(product)}
    >
      <div className="pi-info">
        <span className="pi-name">{product.name}</span>
        <span className="pi-sub">
          {product.generic} / {product.pharmClass}
        </span>
      </div>
      <div className="pi-right">
        <span className="pi-score">Score {formatDecimal(product.score, 0)}</span>
        <span className="pi-sig">{significantCount} significant</span>
      </div>
      <button
        type="button"
        className={`pi-star${product.isFavorite ? ' is-on' : ''}`}
        aria-pressed={product.isFavorite}
        aria-label={`${favoriteLabel} ${product.name}`}
        disabled={isFavoriteBusy || product.isRecentOnly}
        title={favoriteLabel}
        onMouseDown={(event) => {
          // Keep the picker input focused while the button handles the click.
          event.preventDefault();
          event.stopPropagation();
        }}
        onClick={(event) => {
          // Stop propagation so a favorite click does not also select the row.
          event.stopPropagation();
          onToggleFavorite(product);
        }}
      >
        <FavoriteIcon active={product.isFavorite} />
      </button>
    </div>
  );
}

/**************************************************************/
/**
 * Product picker with prototype header trigger, server search, favorites, and recents.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Picker UI.
 */
export function ProductPicker({
  products,
  favoriteProducts,
  recentProducts,
  selectedProduct,
  searchTerm,
  onSearchTermChange,
  onSelectProduct,
  onToggleFavorite,
  favoriteBusyGuids,
  favoriteNotice,
  isLoading,
  error,
  onRetry,
}) {
  // The picker opens from the title trigger or from keyboard interaction.
  const [isOpen, setIsOpen] = useState(false);

  // Active option index tracks keyboard navigation over the flattened rows.
  const [activeIndex, setActiveIndex] = useState(0);

  // Root ref supports outside-click closure.
  const pickerRef = useRef(null);

  // Input ref lets the title trigger move focus into the floating panel.
  const inputRef = useRef(null);

  // Sections are rebuilt only when visible row inputs change.
  const sections = useMemo(
    () => buildPickerSections({ favoriteProducts, recentProducts, products, searchTerm }),
    [favoriteProducts, recentProducts, products, searchTerm],
  );

  // Flattened rows let keyboard handling ignore visual section boundaries.
  const flattenedRows = useMemo(
    () => sections.flatMap((section) => section.rows),
    [sections],
  );

  // The bounded active index prevents stale indexes after result-set changes.
  const boundedActiveIndex = flattenedRows.length === 0
    ? 0
    : Math.min(activeIndex, flattenedRows.length - 1);

  // The active descendant ID is exposed to assistive technology.
  const activeDescendant = flattenedRows[boundedActiveIndex]
    ? `product-option-${flattenedRows[boundedActiveIndex].documentGuid}`
    : undefined;

  // The title trigger mirrors the original prototype when no product is selected.
  const triggerTitle = selectedProduct?.name ?? 'Select product';

  // The metadata row mirrors the prototype's generic, class, and UNII line.
  const triggerMetaItems = selectedProduct
    ? [selectedProduct.generic, selectedProduct.pharmClass, selectedProduct.moiety].filter(Boolean)
    : ['Search product, substance, UNII, or class'];

  useEffect(() => {
    /**************************************************************/
    /**
     * Closes the picker when the user clicks outside it.
     *
     * @param {MouseEvent} event - Browser mouse event.
     */
    function handleDocumentMouseDown(event) {
      // The ref may be null during unmount.
      if (!pickerRef.current) {
        return;
      }

      // Clicks inside the picker should keep the panel open.
      if (pickerRef.current.contains(event.target)) {
        return;
      }

      setIsOpen(false);
    }

    document.addEventListener('mousedown', handleDocumentMouseDown);

    // Cleanup avoids leaking global listeners.
    return () => {
      document.removeEventListener('mousedown', handleDocumentMouseDown);
    };
  }, []);

  useEffect(() => {
    // Opening the floating panel should place keyboard users in the search box.
    if (isOpen) {
      inputRef.current?.focus();
    }
  }, [isOpen]);

  /**************************************************************/
  /**
   * Selects a row from mouse or keyboard input.
   *
   * @param {object} product - Product row to select.
   */
  function commitSelection(product) {
    // Recent-only rows still carry a DocumentGUID and display snapshot.
    if (!product?.documentGuid) {
      return;
    }

    onSelectProduct(product);
    setIsOpen(false);
  }

  /**************************************************************/
  /**
   * Handles combobox keyboard behavior.
   *
   * @param {React.KeyboardEvent<HTMLInputElement>} event - Keyboard event.
   */
  function handleInputKeyDown(event) {
    // ArrowDown opens the panel and moves to the next row.
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setIsOpen(true);
      setActiveIndex((currentIndex) =>
        flattenedRows.length === 0 ? 0 : Math.min(currentIndex + 1, flattenedRows.length - 1),
      );
      return;
    }

    // ArrowUp keeps the active index within bounds.
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      setIsOpen(true);
      setActiveIndex((currentIndex) => Math.max(currentIndex - 1, 0));
      return;
    }

    // Enter commits the active row when the panel is open.
    if (event.key === 'Enter' && isOpen && flattenedRows[boundedActiveIndex]) {
      event.preventDefault();
      commitSelection(flattenedRows[boundedActiveIndex]);
      return;
    }

    // Escape closes the panel without changing selection.
    if (event.key === 'Escape') {
      event.preventDefault();
      setIsOpen(false);
    }
  }

  return (
    <div className="drug-title-wrap" ref={pickerRef}>
      <button
        type="button"
        className={`drug-title${isOpen ? ' is-open' : ''}`}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        onClick={() => {
          // The title behaves like the prototype drug selector trigger.
          setIsOpen((currentValue) => !currentValue);
        }}
      >
        <span>{triggerTitle}</span>
        <svg
          aria-hidden="true"
          className="drug-title-chev"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2.2"
        >
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </button>
      <div className="drug-meta">
        {triggerMetaItems.map((metaItem) => (
          <span key={metaItem} className="drug-meta-item">{metaItem}</span>
        ))}
      </div>

      {isOpen ? (
        <div className="picker" role="presentation">
          <div className="picker-search">
            <svg
              aria-hidden="true"
              className="picker-search-icon"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="2"
            >
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              id="product-search"
              ref={inputRef}
              className="picker-input"
              type="search"
              value={searchTerm}
              placeholder="Search by brand, generic, or class..."
              role="combobox"
              aria-controls="product-picker-listbox"
              aria-expanded={isOpen}
              aria-activedescendant={activeDescendant}
              autoComplete="off"
              onChange={(event) => {
                // Search text is controlled by the dashboard root hook.
                onSearchTermChange(event.target.value);
                setIsOpen(true);
              }}
              onKeyDown={handleInputKeyDown}
            />
            <span className="picker-count">{formatInteger(products.length)} products</span>
          </div>

          <div className="picker-body" id="product-picker-listbox" role="listbox">
            {isLoading ? <Loading label="Loading products" /> : null}
            {error ? <InlineError error={error} onRetry={onRetry} /> : null}
            {!isLoading && !error && flattenedRows.length === 0 ? (
              <EmptyState
                title={searchTerm.trim() ? 'No products match this search.' : 'No dashboard-ready products found.'}
              />
            ) : null}
            {!isLoading && !error
              ? sections.map((section) => (
                  <div className="picker-section" key={section.id}>
                    <div className="picker-section-label">{section.label}</div>
                    {section.rows.map((product) => {
                      // Flattened index controls active styling across sections.
                      const rowIndex = flattenedRows.findIndex(
                        (row) => row.documentGuid === product.documentGuid,
                      );

                      // Selection is based on DocumentGUID only.
                      const isSelected =
                        selectedProduct?.documentGuid?.toLowerCase() === product.documentGuid.toLowerCase();

                      // Favorite busy lookup may be case-sensitive depending on the hook source.
                      const isFavoriteBusy = favoriteBusyGuids.has(product.documentGuid)
                        || favoriteBusyGuids.has(product.documentGuid.toLowerCase());

                      return (
                        <ProductPickerRow
                          key={`${section.id}-${product.documentGuid}`}
                          product={product}
                          isActive={rowIndex === boundedActiveIndex}
                          isSelected={isSelected}
                          isFavoriteBusy={isFavoriteBusy}
                          onSelect={commitSelection}
                          onToggleFavorite={onToggleFavorite}
                        />
                      );
                    })}
                  </div>
                ))
              : null}
          </div>

          <div className="picker-foot">
            {favoriteNotice ? (
              <span>{favoriteNotice}</span>
            ) : (
              <>
                <span><kbd>up</kbd><kbd>down</kbd> move</span>
                <span><kbd>enter</kbd> select</span>
                <span><kbd>star</kbd> favorite</span>
                <span><kbd>esc</kbd> close</span>
              </>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}
