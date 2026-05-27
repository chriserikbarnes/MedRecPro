/* ============================================================
   Cross-product views
   - Symptom → drug reverse lookup (§8)
   - Therapeutic-interchange differential (§9)
   ============================================================ */
const { useState: useStateE, useMemo: useMemoE } = React;

/* Icon */
const IconSearch = (props) =>
<svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="11" cy="11" r="7" /><line x1="21" y1="21" x2="16.65" y2="16.65" />
  </svg>;


/* ============================================================
   REVERSE LOOKUP PANEL
   ============================================================ */
function ReverseLookupPanel() {
  const [symptom, setSymptom] = useStateE('Headache');
  const [query, setQuery] = useStateE('');
  const [drugScope, setDrugScope] = useStateE(DRUGS.map((d) => d.id));

  // Build a name → [{drug, ae}] index across all products
  const allIndex = useMemoE(() => {
    const map = new Map();
    DRUGS.forEach((d) => {
      aeRowsFor(d.id).forEach((ae) => {
        const key = ae.name;
        if (!map.has(key)) map.set(key, []);
        map.get(key).push({ drug: d, ae });
      });
    });
    return map;
  }, []);

  // Symptoms that appear on at least one drug
  const allSymptoms = useMemoE(() => Array.from(allIndex.keys()).sort(), [allIndex]);
  // Symptoms that appear on 2+ drugs (these get featured)
  const commonSymptoms = useMemoE(() =>
  Array.from(allIndex.entries()).
  filter(([, occ]) => occ.length >= 2).
  map(([n]) => n).
  sort((a, b) => allIndex.get(b).length - allIndex.get(a).length).
  slice(0, 10),
  [allIndex]);

  const filteredSymptoms = query.trim() ?
  allSymptoms.filter((n) => n.toLowerCase().includes(query.toLowerCase())).slice(0, 12) :
  commonSymptoms;

  const matches = (allIndex.get(symptom) || []).
  filter(({ drug }) => drugScope.includes(drug.id)).
  sort((a, b) => {
    // 1. Significant elevated first (lowest NNH wins)
    // 2. Then significant protective
    // 3. Then not-significant
    // 4. Then fragile last
    const rank = ({ ae }) => {
      if (ae.prec === 'fragile') return 4;
      if (ae.sig && !ae.prot) return 1;
      if (ae.sig && ae.prot) return 2;
      return 3;
    };
    const ra = rank(a),rb = rank(b);
    if (ra !== rb) return ra - rb;
    return (a.ae.nnh ?? 999999) - (b.ae.nnh ?? 999999);
  });

  const sigCount = matches.filter(({ ae }) => ae.sig && !ae.prot).length;

  const toggleDrug = (id) => {
    setDrugScope(drugScope.includes(id) ? drugScope.filter((x) => x !== id) : [...drugScope, id]);
  };

  return (
    <div className="panel" data-screen-label="Reverse lookup">
      <div className="panel-header">
        <div style={{ flex: 1, minWidth: 0 }}>
          <div className="panel-title">Symptom → drug reverse lookup</div>
          <div className="panel-sub">
            Start from a patient complaint, rank the regimen drugs by how plausibly each one explains it.
          </div>
        </div>
      </div>

      <div className="search-wrap">
        <IconSearch className="search-icon" />
        <input
          className="search-input"
          placeholder="Search adverse-event terms (e.g. dizziness, anemia, flushing)"
          value={query}
          onChange={(e) => setQuery(e.target.value)} />
        
      </div>

      <div className="filter-row">
        <span className="filter-label">{query.trim() ? 'Matches' : 'Common'}</span>
        {filteredSymptoms.length === 0 &&
        <span style={{ fontSize: 12, color: 'var(--color-text-tertiary)', fontStyle: 'italic' }}>
            No symptom matches “{query}”.
          </span>
        }
        {filteredSymptoms.map((s) =>
        <button
          key={s}
          className={'chip' + (s === symptom ? ' active' : '')}
          onClick={() => {setSymptom(s);setQuery('');}}>
          
            {s}
            <span style={{ opacity: .55, marginLeft: 4, fontSize: 11 }}>
              ({allIndex.get(s).length})
            </span>
          </button>
        )}
      </div>

      <div className="filter-row">
        <span className="filter-label">Regimen</span>
        {DRUGS.map((d) => {
          const on = drugScope.includes(d.id);
          return (
            <button
              key={d.id}
              className={'chip' + (on ? ' active' : '')}
              onClick={() => toggleDrug(d.id)}
              title={on ? 'Click to exclude' : 'Click to include'}>
              
              {d.name}
            </button>);

        })}
      </div>

      {matches.length > 0 && sigCount === 0 &&
      <div className="rl-no-sig-banner">
          No drug in the selected regimen shows a <strong>significantly elevated</strong> rate of “{symptom}” vs its comparator.
          Reassure the patient, then investigate non-drug causes.
        </div>
      }

      <div className="rl-results">
        {matches.length === 0 &&
        <div className="rl-empty">
            No drugs in the selected regimen have parsed AE data for “{symptom}”.
          </div>
        }
        {matches.map(({ drug, ae }, i) => {
          const verdict = ae.prec === 'fragile' ? 'low-confidence' :
          ae.sig && !ae.prot ? 'elevated' :
          ae.sig && ae.prot ? 'protective' :
          'not-significant';
          const verdictLabel = {
            'elevated': 'Plausibly causal',
            'protective': 'Protective',
            'not-significant': 'Not significantly elevated',
            'low-confidence': 'Low-confidence signal'
          }[verdict];
          const verdictColor = {
            'elevated': { bg: 'var(--color-orange-light)', fg: 'var(--color-primary-dark)' },
            'protective': { bg: 'var(--color-teal-light)', fg: 'var(--color-teal-deep)' },
            'not-significant': { bg: 'var(--color-surface)', fg: 'var(--color-text-secondary)' },
            'low-confidence': { bg: 'var(--color-brown-light)', fg: 'var(--color-secondary-light)' }
          }[verdict];
          return (
            <div key={drug.id + '-' + i} className={'rl-row' + (ae.prec === 'fragile' ? ' fragile' : '')}>
              <div>
                <div className="rl-drug-name">{drug.name}</div>
                <div className="rl-drug-sub">{drug.pharmClass}</div>
              </div>
              <div className="ae-nnh">
                {ae.nnh != null ?
                <>
                    <div className="ae-nnh-label">NNH 1 in</div>
                    <div className="ae-nnh-value">
                      <span className="ae-nnh-prefix">≈</span>{fmtN(ae.nnh)}
                    </div>
                    <div className="ae-nnh-bounds">[{fmtN(ae.nnhL)} – {ae.nnhH >= 9999 ? '∞' : fmtN(ae.nnhH)}]</div>
                  </> :
                ae.nnt != null ?
                <>
                    <div className="ae-nnh-label">NNT — benefit</div>
                    <div className="ae-nnh-value" style={{ color: 'var(--color-teal-deep)' }}>
                      <span className="ae-nnh-prefix">≈</span>{fmtN(ae.nnt)}
                    </div>
                    <div className="ae-nnh-bounds">[{fmtN(ae.nntL)} – {fmtN(ae.nntH)}]</div>
                  </> :

                <div className="ae-nnh-na">No NNH</div>
                }
              </div>
              <div className="rl-meta">
                <span
                  className="ae-tag"
                  style={{ background: verdictColor.bg, color: verdictColor.fg, borderColor: 'transparent', fontWeight: 'var(--weight-semibold)' }}>
                  
                  {verdictLabel}
                </span>
                <span className="ae-tag rr">RR {fmtRR(ae.rr)} [{fmtRR(ae.rrL)}–{fmtRR(ae.rrH)}]</span>
                <span className={'precision-pill ' + ae.prec}><span className="pip" />{ae.prec}</span>
                {!ae.isPlac && <span className="ae-tag">vs active</span>}
                {ae.combo && <span className="ae-tag combo">Combo</span>}
              </div>
            </div>);

        })}
      </div>
    </div>);

}

/* ============================================================
   INTERCHANGE DIFFERENTIAL PANEL
   ============================================================ */
function InterchangePanel() {
  const [aId, setAId] = useStateE(DRUGS[0].id);
  const [bId, setBId] = useStateE(DRUGS[2].id);
  const [diffOnly, setDiffOnly] = useStateE(false);

  const drugA = expandDrug(aId);
  const drugB = expandDrug(bId);
  const aesA = aeRowsForExpanded(aId);
  const aesB = aeRowsForExpanded(bId);
  const nameA = new Map(aesA.map((x) => [x.name, x]));
  const nameB = new Map(aesB.map((x) => [x.name, x]));
  const allNames = Array.from(new Set([...nameA.keys(), ...nameB.keys()]));

  // Classify rows
  const rows = allNames.map((n) => {
    const ra = nameA.get(n);
    const rb = nameB.get(n);
    let cls;
    if (ra && !rb) cls = 'only-a';else
    if (!ra && rb) cls = 'only-b';else
    {
      const aRR = ra.rr || 1;
      const bRR = rb.rr || 1;
      const logDiff = Math.abs(Math.log10(aRR) - Math.log10(bRR));
      const eitherSig = ra.sig || rb.sig;
      // "similar" if either both non-significant or log-RR difference < 0.15 (~40% rel)
      if (logDiff < 0.15 || !eitherSig) cls = 'similar';else
      if (aRR > bRR) cls = 'a-worse';else
      cls = 'b-worse';
    }
    return { name: n, ra, rb, cls };
  });

  const counts = {
    'a-worse': rows.filter((r) => r.cls === 'a-worse').length,
    'b-worse': rows.filter((r) => r.cls === 'b-worse').length,
    'similar': rows.filter((r) => r.cls === 'similar').length,
    'only-a': rows.filter((r) => r.cls === 'only-a').length,
    'only-b': rows.filter((r) => r.cls === 'only-b').length
  };

  // Group sorted
  const sortByMagA = (r1, r2) => (r2.ra?.rr || 0) - (r1.ra?.rr || 0);
  const sortByMagB = (r1, r2) => (r2.rb?.rr || 0) - (r1.rb?.rr || 0);
  const groups = [
  { id: 'a-worse', label: drugA.name + ' worse on these', items: rows.filter((r) => r.cls === 'a-worse').sort(sortByMagA) },
  { id: 'b-worse', label: drugB.name + ' worse on these', items: rows.filter((r) => r.cls === 'b-worse').sort(sortByMagB) },
  ...(diffOnly ? [] : [{ id: 'similar', label: 'No meaningful difference', items: rows.filter((r) => r.cls === 'similar').sort(sortByMagA) }]),
  { id: 'only-a', label: 'Only on ' + drugA.name, items: rows.filter((r) => r.cls === 'only-a').sort(sortByMagA) },
  { id: 'only-b', label: 'Only on ' + drugB.name, items: rows.filter((r) => r.cls === 'only-b').sort(sortByMagB) }];


  // Log RR axis
  const MIN = 0.1,MAX = 10;
  const xPct = (v) => {
    if (v == null) return null;
    const c = Math.min(Math.max(v, MIN), MAX);
    return (Math.log10(c) - Math.log10(MIN)) / (Math.log10(MAX) - Math.log10(MIN)) * 100;
  };
  const ticks = [0.1, 0.5, 1, 2, 10];

  // Comparator mismatch warning
  const aPlac = aesA.some((x) => x.isPlac);
  const aAct = aesA.some((x) => !x.isPlac);
  const bPlac = aesB.some((x) => x.isPlac);
  const bAct = aesB.some((x) => !x.isPlac);
  const mismatched = aPlac && !bPlac || !aPlac && bPlac || aAct && !bAct || !aAct && bAct;

  // Class mismatch
  const classMismatch = drugA.pharmClass !== drugB.pharmClass;

  return (
    <div className="panel" data-screen-label="Interchange">
      <div className="panel-header">
        <div style={{ flex: 1, minWidth: 0 }}>
          <div className="panel-title">Therapeutic interchange differential</div>
          <div className="panel-sub">
            Switch from one drug to another — see what gets worse, better, stays the same, or appears uniquely on one.
          </div>
        </div>
      </div>

      <div className="ic-pickers">
        <div className="ic-picker">
          <div className="ic-picker-label a"><span className="lbl-dot" /> From — drug A</div>
          <CompactDrugPicker
            drugId={aId}
            catalog={CATALOG}
            onPick={setAId}
            accent="var(--color-primary)"
            disabledIds={[bId]} />
          
        </div>
        <div className="ic-arrow" style={{ padding: "0px", margin: "-20px 0px 20px" }}>→</div>
        <div className="ic-picker">
          <div className="ic-picker-label b"><span className="lbl-dot" /> To — drug B</div>
          <CompactDrugPicker
            drugId={bId}
            catalog={CATALOG}
            onPick={setBId}
            accent="var(--color-teal-deep)"
            disabledIds={[aId]}
            align="right" />
          
        </div>
        <div style={{ display: 'flex', alignItems: 'flex-end', paddingBottom: 2 }}>
          <button className={'chip-toggle' + (diffOnly ? ' on' : '')} onClick={() => setDiffOnly(!diffOnly)}>
            <span className="sw" />
            Differences only
          </button>
        </div>
      </div>

      <div className="ic-summary">
        <div className="ic-summary-cell">
          <div className="ic-summary-num a">{counts['a-worse']}</div>
          <div className="ic-summary-lbl">{drugA.name} worse</div>
        </div>
        <div className="ic-summary-cell">
          <div className="ic-summary-num" style={{ color: 'var(--color-text-tertiary)' }}>{counts['similar']}</div>
          <div className="ic-summary-lbl">No meaningful difference</div>
        </div>
        <div className="ic-summary-cell">
          <div className="ic-summary-num b">{counts['b-worse']}</div>
          <div className="ic-summary-lbl">{drugB.name} worse</div>
        </div>
      </div>

      {(mismatched || classMismatch) &&
      <div className="ic-warn">
          {classMismatch &&
        <>
              <strong>Different pharm classes:</strong> {drugA.pharmClass} vs {drugB.pharmClass}.
              Not a like-for-like substitution.
              {mismatched && ' '}
            </>
        }
          {mismatched &&
        <>
              <strong>Comparator-type mismatch:</strong> the two products do not share the same comparator strata,
              so RRs are not directly comparable on every row.
            </>
        }
        </div>
      }

      <div className="ic-axis">
        <div />
        <div className="ic-axis-ticks">
          {ticks.map((t) =>
          <span key={t} className={'tk' + (t === 1 ? ' ref' : '')} style={{ left: xPct(t) + '%' }}>
              {t}
            </span>
          )}
        </div>
        <div style={{ fontSize: 10, color: 'var(--color-text-tertiary)', textAlign: 'right', alignSelf: 'end' }}>
          DIFFERENCE
        </div>
      </div>

      {groups.map((g) => {
        if (g.items.length === 0) return null;
        return (
          <React.Fragment key={g.id}>
            <div className="ic-divider">
              <span className="ic-divider-text">{g.label} · {g.items.length}</span>
              <span className="line" />
            </div>
            {g.items.map((r, i) =>
            <InterchangeRow key={g.id + '-' + i} row={r} drugA={drugA} drugB={drugB} xPct={xPct} />
            )}
          </React.Fragment>);

      })}
    </div>);

}

function InterchangeRow({ row, drugA, drugB, xPct }) {
  const { ra, rb, cls } = row;
  const renderHalf = (ae, side) => {
    if (!ae) return null;
    const pt = xPct(ae.rr);
    const lo = xPct(ae.rrL);
    const hi = xPct(ae.rrH);
    const isNs = !ae.sig;
    const halfCls = 'ic-track-half ' + side + (isNs ? ' ns' : '');
    return (
      <div className={halfCls}>
        {lo != null && hi != null &&
        <>
            <div className="ic-ci" style={{ left: lo + '%', width: hi - lo + '%' }} />
            <div className="ic-ci-cap" style={{ left: lo + '%' }} />
            <div className="ic-ci-cap" style={{ left: hi + '%' }} />
          </>
        }
        {pt != null && <div className="ic-pt" style={{ left: pt + '%' }} />}
      </div>);

  };

  // Delta label
  let delta = null;
  if (cls === 'a-worse') {
    const r = ra.rr / rb.rr;
    delta = <span className="ic-delta a-worse"><span className="arrow">▲</span>{r.toFixed(1)}× higher on {drugA.name.split(' ')[0]}</span>;
  } else if (cls === 'b-worse') {
    const r = rb.rr / ra.rr;
    delta = <span className="ic-delta b-worse"><span className="arrow">▲</span>{r.toFixed(1)}× higher on {drugB.name.split(' ')[0]}</span>;
  } else if (cls === 'similar') {
    delta = <span className="ic-delta similar">No difference</span>;
  } else if (cls === 'only-a') {
    delta = <span className="ic-delta only">Only A reports</span>;
  } else if (cls === 'only-b') {
    delta = <span className="ic-delta only">Only B reports</span>;
  }

  // Both panes empty? bail
  const soc = (ra || rb).soc;

  return (
    <div className="ic-row">
      <div className="ic-name" title={row.name}>
        {row.name}
        <span className="sub">{soc}</span>
      </div>
      <div className="ic-track">
        <div className="ic-refline" style={{ left: xPct(1) + '%' }} />
        {renderHalf(ra, 'a')}
        {renderHalf(rb, 'b')}
      </div>
      {delta}
    </div>);

}

Object.assign(window, { ReverseLookupPanel, InterchangePanel });