/* ============================================================
   Drug picker — typeahead combobox with persisted recents + favorites.

   Two variants:
     • default — used by the page header (large title trigger)
     • compact — used inline in panels (e.g. Interchange) as a small
                 button trigger with a left accent rule and optional
                 disabledIds for cross-picker exclusivity.

   Persistence (shared across both variants):
     • Recents — last MAX_RECENTS unique selections, LRU eviction
     • Favorites — toggle via star icon, unlimited
   Keyboard inside the panel: ↑ ↓ to move, Enter to commit, Esc to close.
   ============================================================ */

const RECENT_KEY = 'medrecpro:ae:recents';
const FAV_KEY = 'medrecpro:ae:favorites';
const MAX_RECENTS = 12;

function loadList(key) {
  try { return JSON.parse(localStorage.getItem(key) || '[]'); } catch (e) { return []; }
}
function saveList(key, list) {
  try { localStorage.setItem(key, JSON.stringify(list)); } catch (e) { /* ignore */ }
}

function useRecents() {
  const [recents, setRecents] = React.useState(() => loadList(RECENT_KEY));
  React.useEffect(() => {
    const sync = (e) => { if (e.key === RECENT_KEY) setRecents(loadList(RECENT_KEY)); };
    window.addEventListener('storage', sync);
    window.addEventListener('medrecpro:recents-changed', () => setRecents(loadList(RECENT_KEY)));
    return () => {
      window.removeEventListener('storage', sync);
    };
  }, []);
  const push = React.useCallback((id) => {
    setRecents((prev) => {
      const next = [id, ...prev.filter((x) => x !== id)].slice(0, MAX_RECENTS);
      saveList(RECENT_KEY, next);
      window.dispatchEvent(new CustomEvent('medrecpro:recents-changed'));
      return next;
    });
  }, []);
  return [recents, push];
}

function useFavorites() {
  const [favs, setFavs] = React.useState(() => loadList(FAV_KEY));
  React.useEffect(() => {
    const sync = (e) => { if (e.key === FAV_KEY) setFavs(loadList(FAV_KEY)); };
    window.addEventListener('storage', sync);
    window.addEventListener('medrecpro:favorites-changed', () => setFavs(loadList(FAV_KEY)));
    return () => {
      window.removeEventListener('storage', sync);
    };
  }, []);
  const toggle = React.useCallback((id) => {
    setFavs((prev) => {
      const next = prev.includes(id) ? prev.filter((x) => x !== id) : [id, ...prev];
      saveList(FAV_KEY, next);
      window.dispatchEvent(new CustomEvent('medrecpro:favorites-changed'));
      return next;
    });
  }, []);
  return [favs, toggle];
}

function SearchIcon() {
  return (
    <svg className="picker-search-icon" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="11" cy="11" r="7" />
      <path d="m21 21-4.3-4.3" />
    </svg>
  );
}

function HighlightedText({ text, query }) {
  if (!query) return <>{text}</>;
  const idx = text.toLowerCase().indexOf(query.toLowerCase());
  if (idx === -1) return <>{text}</>;
  return (
    <>
      {text.slice(0, idx)}
      <mark className="picker-mark">{text.slice(idx, idx + query.length)}</mark>
      {text.slice(idx + query.length)}
    </>
  );
}

function StarIcon({ filled }) {
  return filled ? (
    <svg viewBox="0 0 24 24" width="15" height="15" fill="currentColor" stroke="none">
      <path d="M12 2l3.09 6.26 6.91 1-5 4.87 1.18 6.88L12 17.77 5.82 21l1.18-6.88-5-4.87 6.91-1L12 2z" />
    </svg>
  ) : (
    <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round">
      <path d="M12 2l3.09 6.26 6.91 1-5 4.87 1.18 6.88L12 17.77 5.82 21l1.18-6.88-5-4.87 6.91-1L12 2z" />
    </svg>
  );
}

/* ────────────────────────────────────────────────────────────
   DrugPickerPanel — the search + recents/favorites + results
   floating panel. Shared by both trigger variants.
   ──────────────────────────────────────────────────────────── */
function DrugPickerPanel({
  catalog,
  currentId,
  onCommit,
  disabledIds, // Set or array of ids that cannot be picked (shows greyed)
  align = 'left', // 'left' | 'right'
  width = 'wide', // 'wide' (560) | 'narrow' (420)
}) {
  const [query, setQuery] = React.useState('');
  const [activeIdx, setActiveIdx] = React.useState(0);
  const [recents, pushRecent] = useRecents();
  const [favorites, toggleFavorite] = useFavorites();
  const inputRef = React.useRef(null);
  const listRef = React.useRef(null);

  const disabledSet = React.useMemo(() => {
    if (!disabledIds) return null;
    return disabledIds instanceof Set ? disabledIds : new Set(disabledIds);
  }, [disabledIds]);

  React.useEffect(() => {
    if (inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, []);

  const sections = React.useMemo(() => {
    const q = query.trim().toLowerCase();
    const byId = new Map(catalog.map((d) => [d.id, d]));
    if (q) {
      const scored = [];
      for (const d of catalog) {
        const n = d.name.toLowerCase();
        const g = (d.generic || '').toLowerCase();
        const c = (d.pharmClass || '').toLowerCase();
        let s = -1;
        if (n.startsWith(q)) s = 0;
        else if (n.includes(q)) s = 1;
        else if (g.startsWith(q)) s = 2;
        else if (g.includes(q)) s = 3;
        else if (c.includes(q)) s = 4;
        if (s >= 0) scored.push({ d, s });
      }
      scored.sort((a, b) => a.s - b.s || a.d.name.localeCompare(b.d.name));
      const items = scored.slice(0, 80).map((x) => x.d);
      return [{ label: `Results · ${scored.length} match${scored.length === 1 ? '' : 'es'}`, items }];
    }
    const out = [];
    const favItems = favorites.map((id) => byId.get(id)).filter(Boolean);
    if (favItems.length) out.push({ label: 'Favorites', items: favItems });
    const recentItems = recents.map((id) => byId.get(id)).filter(Boolean);
    if (recentItems.length) out.push({ label: `Recent · last ${recentItems.length}`, items: recentItems });
    if (out.length === 0) out.push({ label: 'Browse', items: catalog.slice(0, 8) });
    return out;
  }, [query, catalog, recents, favorites]);

  // For keyboard nav, build a flat array of *enabled* items only.
  const flatItems = React.useMemo(
    () => sections.flatMap((s) => s.items).filter((d) => !disabledSet || !disabledSet.has(d.id)),
    [sections, disabledSet]
  );

  React.useEffect(() => { setActiveIdx(0); }, [query]);

  // Keep active row in view
  React.useEffect(() => {
    if (!listRef.current) return;
    const el = listRef.current.querySelector('.picker-item.is-active');
    if (!el) return;
    const box = listRef.current.getBoundingClientRect();
    const elBox = el.getBoundingClientRect();
    if (elBox.top < box.top) listRef.current.scrollTop -= (box.top - elBox.top) + 8;
    else if (elBox.bottom > box.bottom) listRef.current.scrollTop += (elBox.bottom - box.bottom) + 8;
  }, [activeIdx]);

  const commit = (id) => {
    if (disabledSet && disabledSet.has(id)) return;
    onCommit(id);
    pushRecent(id);
  };

  const onKey = (e) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActiveIdx((i) => Math.min(i + 1, Math.max(0, flatItems.length - 1)));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActiveIdx((i) => Math.max(0, i - 1));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const sel = flatItems[activeIdx];
      if (sel) commit(sel.id);
    }
  };

  // Map each enabled item to its active-index. Disabled items get -1.
  const enabledIndex = (() => {
    let i = -1;
    const map = new Map();
    for (const sec of sections) {
      for (const d of sec.items) {
        if (!disabledSet || !disabledSet.has(d.id)) { i += 1; map.set(d.id, i); }
        else map.set(d.id, -1);
      }
    }
    return map;
  })();

  return (
    <div
      className={'picker' + (align === 'right' ? ' picker-align-right' : '') + (width === 'narrow' ? ' picker-narrow' : '')}
      role="listbox"
      onMouseDown={(e) => e.stopPropagation()}
    >
      <div className="picker-search">
        <SearchIcon />
        <input
          ref={inputRef}
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={onKey}
          placeholder="Search by brand, generic, or class…"
          spellCheck={false}
          autoComplete="off"
        />
        <span className="picker-count">{catalog.length.toLocaleString()} products</span>
      </div>
      <div className="picker-body" ref={listRef}>
        {flatItems.length === 0 && (
          <div className="picker-empty">
            No matches for “{query}”. Try a partial brand or generic name.
          </div>
        )}
        {sections.map((sec) => (
          <div className="picker-section" key={sec.label}>
            <div className="picker-section-label">{sec.label}</div>
            {sec.items.map((d) => {
              const idx = enabledIndex.get(d.id);
              const isDisabled = idx === -1;
              const isFav = favorites.includes(d.id);
              const isActive = !isDisabled && idx === activeIdx;
              const isSelected = d.id === currentId;
              return (
                <div
                  key={d.id}
                  className={
                    'picker-item' +
                    (isActive ? ' is-active' : '') +
                    (isSelected ? ' is-selected' : '') +
                    (isDisabled ? ' is-disabled' : '')
                  }
                  role="option"
                  aria-selected={isSelected}
                  aria-disabled={isDisabled || undefined}
                  onMouseEnter={() => !isDisabled && setActiveIdx(idx)}
                  onMouseDown={(e) => { e.preventDefault(); if (!isDisabled) commit(d.id); }}
                >
                  <div className="pi-info">
                    <div className="pi-name">
                      <HighlightedText text={d.name} query={query} />
                      {isSelected && <span className="pi-current">current</span>}
                      {isDisabled && <span className="pi-current pi-disabled-tag">in use</span>}
                    </div>
                    <div className="pi-sub">
                      <HighlightedText text={d.generic} query={query} /> · {d.pharmClass}
                    </div>
                  </div>
                  <div className="pi-right">
                    <span className="pi-score">score {d.score}</span>
                    <button
                      type="button"
                      className={'pi-star' + (isFav ? ' on' : '')}
                      aria-label={isFav ? 'Remove from favorites' : 'Add to favorites'}
                      title={isFav ? 'Unfavorite' : 'Favorite'}
                      onMouseDown={(e) => { e.preventDefault(); e.stopPropagation(); toggleFavorite(d.id); }}
                    >
                      <StarIcon filled={isFav} />
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        ))}
      </div>
      <div className="picker-foot">
        <span><kbd>↑</kbd><kbd>↓</kbd> move</span>
        <span><kbd>⏎</kbd> select</span>
        <span><kbd>★</kbd> favorite</span>
        <span><kbd>esc</kbd> close</span>
      </div>
    </div>
  );
}

/* ────────────────────────────────────────────────────────────
   useOpenWithOutsideClose — generic open-state + outside-click +
   Escape handling.
   ──────────────────────────────────────────────────────────── */
function useOpenWithOutsideClose(ref) {
  const [open, setOpen] = React.useState(false);
  React.useEffect(() => {
    if (!open) return;
    const onDown = (e) => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    const onEsc = (e) => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onEsc);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onEsc);
    };
  }, [open, ref]);
  return [open, setOpen];
}

/* ────────────────────────────────────────────────────────────
   DrugPicker — page-header variant. Big drug title + meta row.
   ──────────────────────────────────────────────────────────── */
function DrugPicker({ drug, catalog, onPick }) {
  const wrapRef = React.useRef(null);
  const [open, setOpen] = useOpenWithOutsideClose(wrapRef);
  const commit = (id) => { onPick(id); setOpen(false); };
  return (
    <div className="drug-title-wrap" ref={wrapRef} style={{ position: 'relative' }}>
      <div
        className={'drug-title' + (open ? ' is-open' : '')}
        onClick={() => setOpen((o) => !o)}
        role="combobox"
        aria-expanded={open}
        aria-haspopup="listbox"
      >
        {drug.name}
        <IconChevDown className="chev" />
      </div>
      <div className="drug-meta">
        <span className="drug-meta-item">{drug.generic}</span>
        <span className="drug-meta-item"><span className="dot" /> {drug.pharmClass}</span>
        <span className="drug-meta-item"><span className="dot" /> {drug.moiety}</span>
      </div>
      {open && (
        <DrugPickerPanel catalog={catalog} currentId={drug.id} onCommit={commit} />
      )}
    </div>
  );
}

/* ────────────────────────────────────────────────────────────
   CompactDrugPicker — small button trigger used inline (e.g. in
   the Interchange panel). Supports an `accent` rule color and a
   `disabledIds` set for cross-picker exclusivity.
   ──────────────────────────────────────────────────────────── */
function CompactDrugPicker({
  drugId,
  catalog,
  onPick,
  accent,         // any CSS color — drawn as the 3px left rule
  disabledIds,    // ids that can't be selected
  placeholder = 'Select product…',
  align = 'left',
}) {
  const wrapRef = React.useRef(null);
  const [open, setOpen] = useOpenWithOutsideClose(wrapRef);
  const drug = catalog.find((d) => d.id === drugId);
  const commit = (id) => { onPick(id); setOpen(false); };
  return (
    <div className="dp-compact-wrap" ref={wrapRef}>
      <button
        type="button"
        className={'dp-compact' + (open ? ' is-open' : '')}
        style={accent ? { borderLeftColor: accent } : null}
        onClick={() => setOpen((o) => !o)}
        role="combobox"
        aria-expanded={open}
        aria-haspopup="listbox"
      >
        <div className="dp-compact-text">
          <div className="dp-compact-name">{drug ? drug.name : placeholder}</div>
          {drug && <div className="dp-compact-sub">{drug.generic}</div>}
        </div>
        <IconChevDown className="dp-compact-chev" />
      </button>
      {open && (
        <DrugPickerPanel
          catalog={catalog}
          currentId={drugId}
          onCommit={commit}
          disabledIds={disabledIds}
          align={align}
        />
      )}
    </div>
  );
}

Object.assign(window, { DrugPicker, CompactDrugPicker, DrugPickerPanel });
