import { useEffect, useMemo, useRef, useState } from 'react';
import { formatInteger } from '../lib/formatters';
import { EmptyState } from './common/EmptyState';
import { InlineError } from './common/InlineError';
import { Loading } from './common/Loading';
import {
  buildSystemSections,
  formatSystemMapCellBadge,
  getSystemPickerChartableCount,
  getSystemPickerDisplayCount,
} from './classPickerHelpers';

/**************************************************************/
/**
 * Builds a safe DOM suffix for a picker option.
 *
 * @param {string} value - Source value.
 * @returns {string} DOM-safe suffix.
 */
function getOptionSuffix(value) {
  return value.toLowerCase().replace(/[^a-z0-9_-]+/g, '-').replace(/^-|-$/g, '') || 'system';
}

/**************************************************************/
/**
 * Renders one MedDRA system option row.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Picker row.
 */
function SystemPickerRow({ item, optionId, isActive, isSelected, onSelect }) {
  return (
    <div
      id={optionId}
      className={`picker-item class-picker-item system-picker-item${isActive ? ' is-active' : ''}${isSelected ? ' is-selected' : ''}`}
      role="option"
      aria-selected={isSelected}
      title={!item.hasRenderableMap ? item.renderabilityReason : undefined}
      onMouseDown={(event) => {
        event.preventDefault();
      }}
      onClick={() => onSelect(item)}
    >
      <div className="pi-info">
        <div className="pi-name">
          <span className="pi-name-text class-name-text">{item.systemOrganClass}</span>
          {isSelected ? <span className="pi-current">selected</span> : null}
        </div>
        <span className="pi-sub">{formatInteger(item.classCount)} classes represented</span>
      </div>
      <div className="pi-right class-picker-meta">
        <span className="pi-score">{formatInteger(item.drugCount)} drugs</span>
        <span className="pi-score">{formatInteger(item.termCount)} terms</span>
        <span className={`ae-tag class-status${item.hasRenderableMap ? ' is-ready' : ' is-small'}`}>
          {formatSystemMapCellBadge(item)}
        </span>
      </div>
    </div>
  );
}

/**************************************************************/
/**
 * MedDRA system picker with live search, keyboard navigation, and multi-select chips.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Picker UI.
 */
export function SystemPicker({
  systems,
  selectedSystems,
  searchTerm,
  onSearchTermChange,
  onAddSystem,
  onRemoveSystem,
  isLoading,
  error,
  onRetry,
  totalSystemCount = 0,
  chartableSystemCount = 0,
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const pickerRef = useRef(null);
  const inputRef = useRef(null);
  const selectedLookup = useMemo(
    () => new Set((selectedSystems ?? []).map((item) => item.systemOrganClass.toLowerCase())),
    [selectedSystems],
  );
  const sections = useMemo(() => buildSystemSections(systems), [systems]);
  const flattenedRows = useMemo(() => sections.flatMap((section) => section.rows), [sections]);
  const boundedActiveIndex = flattenedRows.length === 0 ? 0 : Math.min(activeIndex, flattenedRows.length - 1);
  const activeDescendant = flattenedRows[boundedActiveIndex]
    ? `dashboard-system-option-${getOptionSuffix(flattenedRows[boundedActiveIndex].systemOrganClass)}`
    : undefined;
  const displayCount = getSystemPickerDisplayCount(totalSystemCount, flattenedRows.length);
  const displayChartableCount = getSystemPickerChartableCount(chartableSystemCount, flattenedRows);
  const titleText = selectedSystems.length > 0
    ? selectedSystems.map((system) => system.systemOrganClass).join(', ')
    : 'Select systems';

  useEffect(() => {
    /**************************************************************/
    /**
     * Closes the picker when focus moves outside through mouse input.
     *
     * @param {MouseEvent} event - Browser mouse event.
     */
    function handleDocumentMouseDown(event) {
      if (!pickerRef.current || pickerRef.current.contains(event.target)) {
        return;
      }

      setIsOpen(false);
    }

    document.addEventListener('mousedown', handleDocumentMouseDown);

    return () => {
      document.removeEventListener('mousedown', handleDocumentMouseDown);
    };
  }, []);

  useEffect(() => {
    if (isOpen) {
      inputRef.current?.focus();
    }
  }, [isOpen]);

  /**************************************************************/
  /**
   * Commits a selected MedDRA system.
   *
   * @param {object} item - System row.
   */
  function commitSelection(item) {
    if (!item?.systemOrganClass) {
      return;
    }

    onAddSystem(item);
    setIsOpen(true);
  }

  /**************************************************************/
  /**
   * Handles combobox keyboard controls.
   *
   * @param {React.KeyboardEvent<HTMLInputElement>} event - Keyboard event.
   */
  function handleInputKeyDown(event) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setIsOpen(true);
      setActiveIndex((currentIndex) =>
        flattenedRows.length === 0 ? 0 : Math.min(currentIndex + 1, flattenedRows.length - 1),
      );
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      setIsOpen(true);
      setActiveIndex((currentIndex) => Math.max(currentIndex - 1, 0));
      return;
    }

    if (event.key === 'Enter' && isOpen && flattenedRows[boundedActiveIndex]) {
      event.preventDefault();
      commitSelection(flattenedRows[boundedActiveIndex]);
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      setIsOpen(false);
    }
  }

  return (
    <div className="drug-title-wrap class-title-wrap system-title-wrap" ref={pickerRef}>
      <button
        type="button"
        className={`drug-title class-title system-title${isOpen ? ' is-open' : ''}`}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        title={titleText}
        onClick={() => setIsOpen((currentValue) => !currentValue)}
      >
        <span>{titleText}</span>
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

      <div className="system-chip-row" aria-label="Selected MedDRA systems">
        {selectedSystems.length > 0 ? selectedSystems.map((system) => (
          <span className="chip removable system-chip" key={system.systemOrganClass}>
            {system.systemOrganClass}
            <button
              type="button"
              className="chip-remove"
              aria-label={`Remove ${system.systemOrganClass}`}
              onClick={() => onRemoveSystem(system.systemOrganClass)}
            >
              x
            </button>
          </span>
        )) : (
          <span className="drug-meta-item">Search system organ class</span>
        )}
      </div>

      {isOpen ? (
        <div className="picker class-picker system-picker" role="presentation">
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
              id="system-search"
              ref={inputRef}
              className="picker-input"
              type="text"
              value={searchTerm}
              placeholder="Search system organ class..."
              role="combobox"
              aria-controls="system-picker-listbox"
              aria-expanded={isOpen}
              aria-activedescendant={activeDescendant}
              autoComplete="off"
              spellCheck={false}
              onChange={(event) => {
                onSearchTermChange(event.target.value);
                setIsOpen(true);
              }}
              onKeyDown={handleInputKeyDown}
            />
            <span className="picker-count">
              <span className="picker-count-primary">{formatInteger(displayCount)} systems</span>
              <span className="picker-count-secondary">{formatInteger(displayChartableCount)} chartable</span>
            </span>
          </div>

          <div className="picker-body" id="system-picker-listbox" role="listbox">
            {isLoading ? <Loading label="Loading systems" /> : null}
            {error ? <InlineError error={error} onRetry={onRetry} /> : null}
            {!isLoading && !error && flattenedRows.length === 0 ? (
              <EmptyState title={searchTerm.trim() ? 'No systems match this search.' : 'No MedDRA systems found.'} />
            ) : null}
            {!isLoading && !error
              ? sections.map((section) => (
                  <div className="picker-section" key={section.id}>
                    <div className="picker-section-label">{section.label}</div>
                    {section.rows.map((item) => {
                      const rowIndex = flattenedRows.findIndex(
                        (row) => row.systemOrganClass === item.systemOrganClass,
                      );
                      const isSelected = selectedLookup.has(item.systemOrganClass.toLowerCase());

                      return (
                        <SystemPickerRow
                          key={`${section.id}-${item.systemOrganClass}`}
                          item={item}
                          optionId={`dashboard-system-option-${getOptionSuffix(item.systemOrganClass)}`}
                          isActive={rowIndex === boundedActiveIndex}
                          isSelected={isSelected}
                          onSelect={commitSelection}
                        />
                      );
                    })}
                  </div>
                ))
              : null}
          </div>

          <div className="picker-foot">
            <span><kbd>up</kbd><kbd>down</kbd> move</span>
            <span><kbd>enter</kbd> add</span>
            <span><kbd>esc</kbd> close</span>
          </div>
        </div>
      ) : null}
    </div>
  );
}
