import { useEffect, useMemo, useRef, useState } from 'react';
import { formatInteger } from '../lib/formatters';
import { EmptyState } from './common/EmptyState';
import { InlineError } from './common/InlineError';
import { Loading } from './common/Loading';

/**************************************************************/
/**
 * Builds picker sections for pharmacologic classes.
 *
 * @param {object[]} classes - Class rows.
 * @returns {object[]} Picker sections.
 */
function buildClassSections(classes) {
  const correlatableRows = classes.filter((item) => item.isCorrelatable);
  const smallRows = classes.filter((item) => !item.isCorrelatable);
  const sections = [];

  if (correlatableRows.length > 0) {
    sections.push({ id: 'correlatable', label: 'Correlatable classes', rows: correlatableRows });
  }

  if (smallRows.length > 0) {
    sections.push({ id: 'small', label: 'Too small for map', rows: smallRows });
  }

  return sections;
}

/**************************************************************/
/**
 * Renders one pharmacologic class option row.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Picker row.
 */
function ClassPickerRow({ item, optionId, isActive, isSelected, onSelect }) {
  return (
    <div
      id={optionId}
      className={`picker-item class-picker-item${isActive ? ' is-active' : ''}${isSelected ? ' is-selected' : ''}`}
      role="option"
      aria-selected={isSelected}
      onMouseDown={(event) => {
        event.preventDefault();
      }}
      onClick={() => onSelect(item)}
    >
      <div className="pi-info">
        <div className="pi-name">
          <span className="pi-name-text class-name-text">{item.pharmClassName}</span>
          {isSelected ? <span className="pi-current">current</span> : null}
        </div>
        <span className="pi-sub">{item.pharmClassCode}</span>
      </div>
      <div className="pi-right class-picker-meta">
        <span className="pi-score">{formatInteger(item.drugCount)} drugs</span>
        <span className="pi-score">{formatInteger(item.socCount)} SOCs</span>
        <span className={`ae-tag class-status${item.isCorrelatable ? ' is-ready' : ' is-small'}`}>
          {item.isCorrelatable ? 'correlatable' : 'too small'}
        </span>
      </div>
    </div>
  );
}

/**************************************************************/
/**
 * Class picker with live search and keyboard listbox behavior.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Picker UI.
 */
export function ClassPicker({
  classes,
  selectedClass,
  searchTerm,
  onSearchTermChange,
  onSelectClass,
  isLoading,
  error,
  onRetry,
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const pickerRef = useRef(null);
  const inputRef = useRef(null);
  const sections = useMemo(() => buildClassSections(classes), [classes]);
  const flattenedRows = useMemo(() => sections.flatMap((section) => section.rows), [sections]);
  const boundedActiveIndex = flattenedRows.length === 0 ? 0 : Math.min(activeIndex, flattenedRows.length - 1);
  const activeDescendant = flattenedRows[boundedActiveIndex]
    ? `dashboard-class-option-${flattenedRows[boundedActiveIndex].pharmClassCode}`
    : undefined;

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
   * Commits a selected pharmacologic class.
   *
   * @param {object} item - Class row.
   */
  function commitSelection(item) {
    if (!item?.pharmClassCode) {
      return;
    }

    onSelectClass(item);
    setIsOpen(false);
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
    <div className="drug-title-wrap class-title-wrap" ref={pickerRef}>
      <button
        type="button"
        className={`drug-title class-title${isOpen ? ' is-open' : ''}`}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        onClick={() => setIsOpen((currentValue) => !currentValue)}
      >
        <span>{selectedClass?.pharmClassName ?? 'Select class'}</span>
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
        <span className="drug-meta-item">{selectedClass?.pharmClassCode ?? 'Search class code or name'}</span>
      </div>

      {isOpen ? (
        <div className="picker class-picker" role="presentation">
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
              id="class-search"
              ref={inputRef}
              className="picker-input"
              type="text"
              value={searchTerm}
              placeholder="Search class code or name..."
              role="combobox"
              aria-controls="class-picker-listbox"
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
            <span className="picker-count">{formatInteger(flattenedRows.length)} classes</span>
          </div>

          <div className="picker-body" id="class-picker-listbox" role="listbox">
            {isLoading ? <Loading label="Loading classes" /> : null}
            {error ? <InlineError error={error} onRetry={onRetry} /> : null}
            {!isLoading && !error && flattenedRows.length === 0 ? (
              <EmptyState title={searchTerm.trim() ? 'No classes match this search.' : 'No AE classes found.'} />
            ) : null}
            {!isLoading && !error
              ? sections.map((section) => (
                  <div className="picker-section" key={section.id}>
                    <div className="picker-section-label">{section.label}</div>
                    {section.rows.map((item) => {
                      const rowIndex = flattenedRows.findIndex(
                        (row) => row.pharmClassCode === item.pharmClassCode,
                      );
                      const isSelected =
                        selectedClass?.pharmClassCode?.toLowerCase() === item.pharmClassCode.toLowerCase();

                      return (
                        <ClassPickerRow
                          key={`${section.id}-${item.pharmClassCode}`}
                          item={item}
                          optionId={`dashboard-class-option-${item.pharmClassCode}`}
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
            <span><kbd>enter</kbd> select</span>
            <span><kbd>esc</kbd> close</span>
          </div>
        </div>
      ) : null}
    </div>
  );
}
