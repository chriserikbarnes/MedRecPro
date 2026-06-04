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
    // Carry live favorite state so the recent row's star renders filled when the
    // product is already a favorite and toggling computes the correct next state.
    isFavorite: recent.isFavorite,
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

    // The prototype only shows a browse section when no personalized rows exist.
    if (sections.length === 0) {
      addSection('browse', 'Browse', products.slice(0, 8));
    }
  } else {
    const matchCount = products.length;
    const matchLabel = `Results - ${formatInteger(matchCount)} match${matchCount === 1 ? '' : 'es'}`;
    addSection('results', matchLabel, products);
  }

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
  if (active) {
    return (
      <svg
        aria-hidden="true"
        className="pi-star-icon"
        viewBox="0 0 24 24"
        fill="currentColor"
        stroke="none"
      >
        <path d="M12 2l3.09 6.26 6.91 1-5 4.87 1.18 6.88L12 17.77 5.82 21l1.18-6.88-5-4.87 6.91-1L12 2z" />
      </svg>
    );
  }

  return (
    <svg
      aria-hidden="true"
      className="pi-star-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeLinejoin="round"
      strokeWidth="1.7"
    >
      <path d="M12 2l3.09 6.26 6.91 1-5 4.87 1.18 6.88L12 17.77 5.82 21l1.18-6.88-5-4.87 6.91-1L12 2z" />
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
  optionId,
  isActive,
  isSelected,
  isDisabled = false,
  onSelect,
  onToggleFavorite,
  isFavoriteBusy,
}) {
  // Favorite action reflects the visible row state.
  const favoriteLabel = formatFavoriteAction(product.isFavorite);

  return (
    <div
      id={optionId}
      className={`picker-item${isActive ? ' is-active' : ''}${isSelected ? ' is-selected' : ''}${isDisabled ? ' is-disabled' : ''}`}
      role="option"
      aria-selected={isSelected}
      aria-disabled={isDisabled}
      onMouseDown={(event) => {
        // Prevent input blur before selection commits.
        event.preventDefault();
      }}
      onClick={() => {
        if (!isDisabled) {
          onSelect(product);
        }
      }}
    >
      <div className="pi-info">
        <div className="pi-name">
          <span className="pi-name-text">{product.name}</span>
          {isSelected ? <span className="pi-current">current</span> : null}
          {isDisabled && !isSelected ? <span className="ae-tag pi-disabled-tag">in use</span> : null}
        </div>
        <span className="pi-sub">
          {product.generic} - {product.pharmClass}
        </span>
      </div>
      <div className="pi-right">
        <span className="pi-score">score {formatDecimal(product.score, 0)}</span>
        <button
          type="button"
          className={`pi-star${product.isFavorite ? ' is-on' : ''}`}
          aria-pressed={product.isFavorite}
          aria-label={`${favoriteLabel} ${product.name}`}
          disabled={isFavoriteBusy || isDisabled}
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
    </div>
  );
}

/**************************************************************/
/**
 * Builds the vertical product metadata lines shown under the picker title.
 *
 * @param {object | null} selectedProduct - Selected product view model.
 * @returns {string[]} One line per active ingredient (paired with its EPC class), then the UNII.
 */
function buildTriggerMetaItems(selectedProduct) {
  // No product shows the search hint, matching the prototype's empty state.
  if (!selectedProduct) {
    return ['Search product, substance, UNII, or class'];
  }

  // Combination products carry one entry per active ingredient.
  const ingredients = Array.isArray(selectedProduct.activeIngredients)
    ? selectedProduct.activeIngredients
    : [];

  if (ingredients.length > 0) {
    // Each ingredient is paired with its standardized EPC class on its own line.
    const ingredientLines = ingredients.map((ingredient) =>
      ingredient.pharmClass
        ? `${ingredient.substance} - ${ingredient.pharmClass}`
        : ingredient.substance,
    );

    return [...ingredientLines, selectedProduct.moiety].filter(Boolean);
  }

  // Fallback to the legacy single-line metadata when no ingredient list exists.
  return [selectedProduct.generic, selectedProduct.pharmClass, selectedProduct.moiety].filter(Boolean);
}

/**************************************************************/
/**
 * Compact product picker for inline two-product tools.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Compact picker UI.
 */
export function CompactProductPicker({
  idPrefix,
  tone = '',
  align = 'left',
  products,
  favoriteProducts = [],
  recentProducts = [],
  totalProductCount = 0,
  selectedProduct,
  disabledDocumentGuid = '',
  searchTerm,
  onSearchTermChange,
  onSelectProduct,
  onToggleFavorite,
  favoriteBusyGuids = new Set(),
  favoriteNotice = '',
  isLoading,
  error,
  onRetry,
}) {
  // Compact pickers use the same floating product menu as the page header.
  const [isOpen, setIsOpen] = useState(false);

  // Active option index tracks keyboard navigation over the flattened rows.
  const [activeIndex, setActiveIndex] = useState(0);

  // Root ref supports outside-click closure.
  const pickerRef = useRef(null);

  // Input ref lets the compact trigger move focus into the floating panel.
  const inputRef = useRef(null);

  // Sections match the main product picker: favorites, recents, browse, or results.
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

  // Prefix IDs so two compact pickers can be open without duplicate active descendants.
  const optionIdPrefix = idPrefix || `compact-product-${tone || 'picker'}`;
  const activeDescendant = flattenedRows[boundedActiveIndex]
    ? `${optionIdPrefix}-option-${flattenedRows[boundedActiveIndex].documentGuid}`
    : undefined;
  const selectedMeta = buildTriggerMetaItems(selectedProduct);
  const triggerTitle = selectedProduct?.name ?? 'Select product';
  const triggerSub = selectedProduct ? selectedMeta[0] : selectedMeta[0];
  const disabledLookupKey = disabledDocumentGuid?.toLowerCase() ?? '';
  const pickerAlignmentClass = align === 'right' ? ' picker-align-right' : '';

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
    // Same-product rows are visible for context but cannot be selected.
    if (!product?.documentGuid || product.documentGuid.toLowerCase() === disabledLookupKey) {
      return;
    }

    onSelectProduct(product);
    setIsOpen(false);
  }

  /**************************************************************/
  /**
   * Handles compact combobox keyboard behavior.
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

    // Asterisk toggles the active row's favorite.
    if (event.key === '*') {
      event.preventDefault();

      const activeRow = flattenedRows[boundedActiveIndex];
      if (activeRow && activeRow.documentGuid.toLowerCase() !== disabledLookupKey) {
        onToggleFavorite(activeRow);
      }

      return;
    }

    // Escape closes the panel without changing selection.
    if (event.key === 'Escape') {
      event.preventDefault();
      setIsOpen(false);
    }
  }

  return (
    <div className="dp-compact-wrap" ref={pickerRef}>
      <button
        type="button"
        className={`dp-compact ${tone}${isOpen ? ' is-open' : ''}`}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        onClick={() => {
          // The compact trigger opens the same product-picker menu.
          setIsOpen((currentValue) => !currentValue);
        }}
      >
        <span className="dp-compact-text">
          <span className="dp-compact-name">{triggerTitle}</span>
          <span className="dp-compact-sub">{triggerSub}</span>
        </span>
        <svg
          aria-hidden="true"
          className="dp-compact-chev"
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

      {isOpen ? (
        <div className={`picker picker-narrow${pickerAlignmentClass}`} role="presentation">
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
              <circle cx="11" cy="11" r="7" />
              <path d="m21 21-4.3-4.3" />
            </svg>
            <input
              id={`${optionIdPrefix}-search`}
              ref={inputRef}
              className="picker-input"
              type="text"
              value={searchTerm}
              placeholder="Search by brand, generic, or class..."
              role="combobox"
              aria-controls={`${optionIdPrefix}-listbox`}
              aria-expanded={isOpen}
              aria-activedescendant={activeDescendant}
              autoComplete="off"
              spellCheck={false}
              onChange={(event) => {
                // Search text is controlled by the dashboard root hook.
                onSearchTermChange(event.target.value);
                setIsOpen(true);
              }}
              onKeyDown={handleInputKeyDown}
            />
            <span className="picker-count">{formatInteger(totalProductCount)} products</span>
          </div>

          <div className="picker-body" id={`${optionIdPrefix}-listbox`} role="listbox">
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
                      const isDisabled =
                        Boolean(disabledLookupKey)
                        && !isSelected
                        && product.documentGuid.toLowerCase() === disabledLookupKey;

                      // Favorite busy lookup may be case-sensitive depending on the hook source.
                      const isFavoriteBusy = favoriteBusyGuids.has(product.documentGuid)
                        || favoriteBusyGuids.has(product.documentGuid.toLowerCase());

                      return (
                        <ProductPickerRow
                          key={`${section.id}-${product.documentGuid}`}
                          product={product}
                          optionId={`${optionIdPrefix}-option-${product.documentGuid}`}
                          isActive={rowIndex === boundedActiveIndex}
                          isSelected={isSelected}
                          isDisabled={isDisabled}
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
                <span><kbd>*</kbd> favorite</span>
                <span><kbd>esc</kbd> close</span>
              </>
            )}
          </div>
        </div>
      ) : null}
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
  totalProductCount,
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
    ? `dashboard-product-option-${flattenedRows[boundedActiveIndex].documentGuid}`
    : undefined;

  // The title trigger mirrors the original prototype when no product is selected.
  const triggerTitle = selectedProduct?.name ?? 'Select product';

  // One line per active ingredient (paired with its EPC class), then the UNII.
  const triggerMetaItems = buildTriggerMetaItems(selectedProduct);

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

    // Asterisk toggles the active row's favorite, matching the "* favorite" footer
    // hint. It is intercepted so the character never lands in the search box.
    if (event.key === '*') {
      event.preventDefault();

      // Only act when a row is available to favorite.
      const activeRow = flattenedRows[boundedActiveIndex];
      if (activeRow) {
        onToggleFavorite(activeRow);
      }

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
        {triggerMetaItems.map((metaItem, index) => (
          <span key={`${index}-${metaItem}`} className="drug-meta-item">{metaItem}</span>
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
              <circle cx="11" cy="11" r="7" />
              <path d="m21 21-4.3-4.3" />
            </svg>
            <input
              id="product-search"
              ref={inputRef}
              className="picker-input"
              type="text"
              value={searchTerm}
              placeholder="Search by brand, generic, or class..."
              role="combobox"
              aria-controls="product-picker-listbox"
              aria-expanded={isOpen}
              aria-activedescendant={activeDescendant}
              autoComplete="off"
              spellCheck={false}
              onChange={(event) => {
                // Search text is controlled by the dashboard root hook.
                onSearchTermChange(event.target.value);
                setIsOpen(true);
              }}
              onKeyDown={handleInputKeyDown}
            />
            <span className="picker-count">{formatInteger(totalProductCount)} products</span>
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
                          optionId={`dashboard-product-option-${product.documentGuid}`}
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
                <span><kbd>*</kbd> favorite</span>
                <span><kbd>esc</kbd> close</span>
              </>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}
