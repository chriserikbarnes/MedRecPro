/* ============================================================
   Views: Triage, Forest, Quadrant + main App
   ============================================================ */
const { useState: useStateV, useMemo: useMemoV } = React;

/* ============================================================
   TRIAGE VIEW (§5)
   ============================================================ */
function TriageView({ aes, showFragile, compFilter }) {
  const [expanded, setExpanded] = useStateV(null);

  // Filter
  const filtered = aes.filter(ae => {
    if (compFilter === 'placebo' && !ae.isPlac) return false;
    if (compFilter === 'active' && ae.isPlac) return false;
    return true;
  });

  // Group by tier
  const grouped = TIERS.map(t => ({
    ...t,
    items: filtered
      .filter(ae => tierFor(ae) === t.id)
      .sort((a, b) => (a.nnh ?? a.nnt ?? 99999) - (b.nnh ?? b.nnt ?? 99999)),
  }));

  return (
    <div>
      {grouped.map(t => {
        if (t.id === 'fragile' && !showFragile) return null;
        if (t.items.length === 0) return null;
        return (
          <div key={t.id} className={'tier tier-' + t.id}>
            <div className="tier-header">
              <div className="tier-marker" />
              <div className="tier-meta">
                <div className="tier-name">{t.name}</div>
                <div className="tier-desc">{t.desc}</div>
              </div>
              <div className="tier-count">{t.items.length}</div>
            </div>
            {t.items.map((ae, i) => (
              <AeRow
                key={ae.name + i}
                ae={ae}
                tier={t.id}
                expanded={expanded === ae.name}
                onClick={() => setExpanded(expanded === ae.name ? null : ae.name)}
              />
            ))}
          </div>
        );
      })}
    </div>
  );
}

function AeRow({ ae, tier, expanded, onClick }) {
  const n = ae.type === 'NNT' ? ae.nnt : ae.nnh;
  const nL = ae.type === 'NNT' ? ae.nntL : ae.nnhL;
  const nH = ae.type === 'NNT' ? ae.nntH : ae.nnhH;
  const eventRate = ((ae.eT / ae.armN) * 100).toFixed(1);
  const compRate = ((ae.eC / ae.armN) * 100).toFixed(1);
  const isSerious = SOC_SERIOUS.has(ae.soc) && ae.sig;

  return (
    <div
      className={'ae-row ' + (ae.prec === 'fragile' ? 'fragile' : '') + (expanded ? ' expanded' : '')}
      onClick={onClick}
    >
      <div className="ae-nnh">
        {n != null ? (
          <>
            <div className="ae-nnh-label">{ae.type === 'NNT' ? 'NNT — benefit 1 in' : 'NNH — harm 1 in'}</div>
            <div className="ae-nnh-value">
              <span className="ae-nnh-prefix">≈</span>{fmtN(n)}
            </div>
            <div className="ae-nnh-bounds">[{fmtN(nL)} – {nH >= 9999 ? '∞' : fmtN(nH)}]</div>
          </>
        ) : (
          <div className="ae-nnh-na">No NNH<br/><span style={{ fontSize: 11 }}>(not significant)</span></div>
        )}
      </div>
      <div className="ae-body">
        <div className="ae-name">{ae.name}</div>
        <div className="ae-meta">
          <span className="ae-tag soc">{ae.soc}</span>
          <span className="ae-tag rr">RR {fmtRR(ae.rr)} [{fmtRR(ae.rrL)}–{fmtRR(ae.rrH)}]</span>
          {isSerious && <span className="ae-tag serious">Serious SOC</span>}
          {!ae.isPlac && <span className="ae-tag">vs active comparator</span>}
          {ae.combo && <span className="ae-tag combo">Combination product</span>}
          {(ae.flags || []).map(f => <span key={f} className="ae-tag flag">{f}</span>)}
        </div>
      </div>
      <div className="ae-right">
        <span className={'precision-pill ' + ae.prec}>
          <span className="pip" />
          {ae.prec}
        </span>
        <span className="ae-expand">{expanded ? '−' : '+'} details</span>
      </div>
      {expanded && (
        <div className="ae-detail">
          <div className="ae-detail-cell">
            <span className="lbl">Treatment events</span>
            <span className="val">{ae.eT} / {ae.armN.toLocaleString()} <span style={{ color: 'var(--color-text-tertiary)' }}>({eventRate}%)</span></span>
          </div>
          <div className="ae-detail-cell">
            <span className="lbl">Comparator events</span>
            <span className="val">{ae.eC} / {ae.armN.toLocaleString()} <span style={{ color: 'var(--color-text-tertiary)' }}>({compRate}%)</span></span>
          </div>
          <div className="ae-detail-cell">
            <span className="lbl">Risk type</span>
            <span className="val">{ae.prot ? 'Protective' : ae.sig ? 'Elevated' : 'Not significant'}</span>
          </div>
          <div className="ae-detail-cell">
            <span className="lbl">Comparator</span>
            <span className="val">{ae.isPlac ? 'Placebo' : 'Active'}</span>
          </div>
          {ae.flags && ae.flags.length > 0 && (
            <div className="ae-detail-cell" style={{ gridColumn: '1 / -1' }}>
              <span className="lbl">Why this row is low confidence</span>
              <span className="val wide">
                {ae.flags.map(f => FLAG_TEXT[f] || f).join(' ')}
              </span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/* ============================================================
   FOREST PLOT (§7)
   - log RR x-axis from 0.1 to 10
   ============================================================ */
function ForestView({ aes, showFragile }) {
  // Show fragile rows desaturated regardless of toggle but allow user to hide.
  const rows = aes.filter(ae => showFragile || ae.prec !== 'fragile');

  // Log scale domain
  const MIN = 0.1;
  const MAX = 10;
  const logMin = Math.log10(MIN);
  const logMax = Math.log10(MAX);
  const xPct = v => {
    if (v == null) return null;
    const c = Math.min(Math.max(v, MIN), MAX);
    return ((Math.log10(c) - logMin) / (logMax - logMin)) * 100;
  };

  const ticks = [0.1, 0.25, 0.5, 1, 2, 4, 10];

  // Sort by RR descending so elevated signals are on top
  const sorted = [...rows].sort((a, b) => (b.rr || 0) - (a.rr || 0));

  return (
    <div>
      <div className="forest-legend">
        <span className="forest-legend-item">
          <span className="forest-legend-dot" style={{ background: 'var(--color-primary)' }} />
          Elevated risk
        </span>
        <span className="forest-legend-item">
          <span className="forest-legend-dot" style={{ background: 'var(--color-teal-deep)' }} />
          Protective
        </span>
        <span className="forest-legend-item">
          <span className="forest-legend-dot" style={{ background: '#fff', borderColor: 'var(--color-text-tertiary)' }} />
          Not significant
        </span>
        <span className="forest-legend-item" style={{ color: 'var(--color-text-tertiary)' }}>
          ← Protective &nbsp;·&nbsp; RR=1 ref line &nbsp;·&nbsp; Elevated →
        </span>
      </div>
      <div className="forest-wrap">
        <div className="forest-axis">
          <div className="forest-axis-spacer" />
          <div className="forest-axis-ticks">
            {ticks.map(t => (
              <span key={t} className={'forest-tick' + (t === 1 ? ' ref' : '')} style={{ left: xPct(t) + '%' }}>
                {t}
              </span>
            ))}
          </div>
        </div>
        {sorted.map((ae, i) => {
          const dir = !ae.sig ? 'ns' : ae.prot ? 'protective' : 'elevated';
          const pt = xPct(ae.rr);
          const lo = xPct(ae.rrL);
          const hi = xPct(ae.rrH);
          const refX = xPct(1);
          return (
            <div key={ae.name + i} className={'forest-row ' + dir + (ae.prec === 'fragile' ? ' fragile' : '')}>
              <div className="forest-label" title={ae.name}>
                {ae.name}
                <span className="sub">{ae.soc}</span>
              </div>
              <div className="forest-track">
                <div className="forest-refline" style={{ left: refX + '%' }} />
                {lo != null && hi != null && (
                  <>
                    <div className="forest-ci" style={{ left: lo + '%', width: (hi - lo) + '%' }} />
                    <div className="forest-ci-cap" style={{ left: lo + '%' }} />
                    <div className="forest-ci-cap" style={{ left: hi + '%' }} />
                  </>
                )}
                {pt != null && <div className="forest-pt" style={{ left: pt + '%' }} />}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ============================================================
   RISK-VS-PRECISION QUADRANT (§14)
   x = precision (narrow CI = right), y = |log RR| (high = top)
   ============================================================ */
function QuadrantView({ aes, showFragile }) {
  const [hover, setHover] = useStateV(null);
  const rows = aes.filter(ae => showFragile || ae.prec !== 'fragile');

  // Compute precision = -log10(CI width on log scale) — bounded
  // y = log10(RR) magnitude, signed
  const points = rows.map(ae => {
    const logCIWidth = Math.log10(ae.rrH) - Math.log10(ae.rrL);
    // Precision: 0 (wide) → 1 (tight). Cap at 3 log-units wide.
    const precision = Math.max(0, Math.min(1, 1 - logCIWidth / 3));
    const yLog = Math.log10(ae.rr || 1);
    // Map y from [-1.5, 1.5] to [0, 1]
    const y = Math.max(0, Math.min(1, (yLog + 1.5) / 3));
    // Bubble size by armN events (sqrt)
    const size = 8 + Math.sqrt(ae.eT + ae.eC) * 1.6;
    const dir = !ae.sig ? 'ns' : ae.prot ? 'protective' : 'elevated';
    return { ae, x: precision, y, size, dir };
  });

  return (
    <div className="quadrant-wrap">
      <div style={{ position: 'relative', paddingLeft: 18 }}>
        <div className="axis-y">Effect magnitude →</div>
        <div className="quadrant">
          <div className="q-cell tl">
            <span className="q-cell-label">Top-left</span>
            <span className="q-cell-name">Investigate — big but uncertain</span>
          </div>
          <div className="q-cell tr">
            <span className="q-cell-label">Top-right</span>
            <span className="q-cell-name">Warn — big and certain</span>
          </div>
          <div className="q-cell bl">
            <span className="q-cell-label">Bottom-left</span>
            <span className="q-cell-name">Ignore — small and noisy</span>
          </div>
          <div className="q-cell br">
            <span className="q-cell-label">Bottom-right</span>
            <span className="q-cell-name">Reassure — small and certain</span>
          </div>
          {points.map((p, i) => (
            <div
              key={p.ae.name + i}
              className={'q-dot ' + p.dir}
              style={{
                left: (p.x * 92 + 4) + '%',
                top: ((1 - p.y) * 92 + 4) + '%',
                width: p.size,
                height: p.size,
                opacity: p.ae.prec === 'fragile' ? 0.45 : 0.92,
              }}
              onMouseEnter={() => setHover(p)}
              onMouseLeave={() => setHover(null)}
              onTouchStart={() => setHover(p)}
            />
          ))}
          {hover && (
            <div
              className="q-tooltip"
              style={{
                left: (hover.x * 92 + 4) + '%',
                top: ((1 - hover.y) * 92 + 4) + '%',
              }}
            >
              <strong>{hover.ae.name}</strong>
              <div className="small">RR {fmtRR(hover.ae.rr)} [{fmtRR(hover.ae.rrL)}–{fmtRR(hover.ae.rrH)}]</div>
              <div className="small">{hover.ae.eT}/{hover.ae.armN} vs {hover.ae.eC}/{hover.ae.armN}</div>
            </div>
          )}
        </div>
        <div className="quadrant-axes">
          <span>← Lower precision (wide CI)</span>
          <span style={{ textAlign: 'right' }}>Higher precision (tight CI) →</span>
        </div>
      </div>
      <div className="forest-legend" style={{ paddingTop: 16 }}>
        <span className="forest-legend-item">
          <span className="forest-legend-dot" style={{ background: 'var(--color-primary)' }} />
          Elevated risk
        </span>
        <span className="forest-legend-item">
          <span className="forest-legend-dot" style={{ background: 'var(--color-teal-deep)' }} />
          Protective
        </span>
        <span className="forest-legend-item">
          <span className="forest-legend-dot" style={{ background: 'var(--color-text-tertiary)', opacity: 0.6 }} />
          Not significant
        </span>
        <span className="forest-legend-item" style={{ color: 'var(--color-text-tertiary)' }}>
          Bubble size ∝ √(events)
        </span>
      </div>
    </div>
  );
}

/* ============================================================
   MAIN APP
   ============================================================ */
function App() {
  const [drugId, setDrugId] = useStateV(DRUGS[0].id);
  const [view, setView] = useStateV('triage');
  const [showFragile, setShowFragile] = useStateV(true);
  const [compFilter, setCompFilter] = useStateV('all');

  const drug = expandDrug(drugId);
  const aes = aeRowsForExpanded(drugId);

  const fragileCount = aes.filter(ae => ae.prec === 'fragile').length;
  const placeboCount = aes.filter(ae => ae.isPlac).length;
  const activeCount = aes.length - placeboCount;

  const viewTitle = {
    triage:   { t: 'Counseling priority',         s: 'Adverse events sorted into action tiers — most likely harm first.' },
    forest:   { t: 'Forest plot',                 s: 'Relative risk with confidence intervals on a log scale.' },
    quadrant: { t: 'Risk-vs-precision quadrant',  s: 'Effect magnitude on the y-axis, estimate precision on the x-axis.' },
  }[view];

  return (
    <div className="app" data-screen-label="AE Dashboard">
      <TopBar />
      <PageHeader drug={drug} drugs={CATALOG} onPick={setDrugId} />
      <KpiStrip drug={drug} />

      <div className="panel" data-screen-label="AE primary view">
        <div className="panel-header">
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="panel-title">{viewTitle.t}</div>
            <div className="panel-sub">{viewTitle.s}</div>
          </div>
          <div className="tabs" role="tablist">
            <button className={'tab' + (view === 'triage' ? ' active' : '')} onClick={() => setView('triage')}>Triage</button>
            <button className={'tab' + (view === 'forest' ? ' active' : '')} onClick={() => setView('forest')}>Forest</button>
            <button className={'tab' + (view === 'quadrant' ? ' active' : '')} onClick={() => setView('quadrant')}>Quadrant</button>
          </div>
        </div>

        <div className="filter-row">
          <span className="filter-label">Comparator</span>
          <button className={'chip' + (compFilter === 'all' ? ' active' : '')} onClick={() => setCompFilter('all')}>All ({aes.length})</button>
          <button className={'chip' + (compFilter === 'placebo' ? ' active' : '')} onClick={() => setCompFilter('placebo')} disabled={placeboCount === 0} style={placeboCount === 0 ? { opacity: 0.4, cursor: 'not-allowed' } : null}>Placebo ({placeboCount})</button>
          <button className={'chip' + (compFilter === 'active' ? ' active' : '')} onClick={() => setCompFilter('active')} disabled={activeCount === 0} style={activeCount === 0 ? { opacity: 0.4, cursor: 'not-allowed' } : null}>Active comparator ({activeCount})</button>
          <span style={{ width: 12 }} />
          <button className={'chip-toggle' + (showFragile ? ' on' : '')} onClick={() => setShowFragile(!showFragile)}>
            <span className="sw" />
            Show fragile rows ({fragileCount})
          </button>
        </div>

        {view === 'triage'   && <TriageView   aes={aes} showFragile={showFragile} compFilter={compFilter} />}
        {view === 'forest'   && <ForestView   aes={aes.filter(a => compFilter === 'all' || (compFilter === 'placebo' ? a.isPlac : !a.isPlac))} showFragile={showFragile} />}
        {view === 'quadrant' && <QuadrantView aes={aes.filter(a => compFilter === 'all' || (compFilter === 'placebo' ? a.isPlac : !a.isPlac))} showFragile={showFragile} />}
      </div>

      <div className="section-heading">
        <span className="section-heading-text">Cross-product tools</span>
        <span className="line" />
      </div>

      <ReverseLookupPanel />
      <InterchangePanel />

      <div className="foot-note">
        Data shown: <code>tmp_FlattenedAdverseEventRiskTable</code> projection for the selected product.
        Per §3.4 of the visualization plan, fragile rows render desaturated and never enter the “Expect &amp; counsel” tier.
        NNH is the primary metric; RR with log-CI is secondary. Combination-product rows carry an attribution caveat.
        <br /><br />
        <em>Prototype — fictional drugs with realistic but synthetic adverse-event statistics. Not for clinical use.</em>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
