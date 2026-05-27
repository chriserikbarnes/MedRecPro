/* ============================================================
   MedRecPro Adverse Events Dashboard
   - Inventory coverage layer (§4)
   - Warning triage / counseling-priority list (§5) — flagship
   - Forest plot (§7)
   - Risk-vs-precision quadrant (§14)
   ============================================================ */
const { useState, useMemo, useRef, useEffect } = React;

/* ============================================================
   DRUG CATALOG — fictional examples (no branded UI)
   ============================================================ */
const DRUGS = [
  {
    id: 'norvexis-25',
    name: 'Norvexis 25 mg',
    generic: 'norvexamine succinate',
    moiety: 'UNII: 7K3X9P2Q4R',
    pharmClass: 'Selective NE/5-HT modulator',
    armN: 850,
    comparatorN: 842,
    rowCount: 47,
    significant: 12,
    significantProtective: 1,
    placeboCoverage: true,
    activeCoverage: true,
    doseCoverage: 0.64,
    socBreadth: 11,
    socTotal: 17,
    monoComboMix: 'mono',
    score: 82,
    scoreReason: 'Strong placebo-controlled coverage, multiple tight signals',
  },
  {
    id: 'olemitra-100',
    name: 'Olemitra 100 mg',
    generic: 'olemitravir',
    moiety: 'UNII: 4M2L8K9X1A',
    pharmClass: 'Direct-acting antiviral (NS5A inhibitor)',
    armN: 412,
    comparatorN: 408,
    rowCount: 28,
    significant: 5,
    significantProtective: 0,
    placeboCoverage: false,
    activeCoverage: true,
    doseCoverage: 0.20,
    socBreadth: 8,
    socTotal: 17,
    monoComboMix: 'combo',
    score: 54,
    scoreReason: 'Active-comparator only, ~17% combination product rows',
  },
  {
    id: 'kavrolide-er',
    name: 'Kavrolide ER 200 mg',
    generic: 'kavrolide hydrochloride',
    moiety: 'UNII: 9N1P5R2S8T',
    pharmClass: 'Calcium channel blocker (L-type)',
    armN: 1240,
    comparatorN: 1235,
    rowCount: 62,
    significant: 18,
    significantProtective: 3,
    placeboCoverage: true,
    activeCoverage: true,
    doseCoverage: 0.81,
    socBreadth: 13,
    socTotal: 17,
    monoComboMix: 'mono',
    score: 91,
    scoreReason: 'Broad SOC coverage, strong dose data, mostly tight precision',
  },
  {
    id: 'tracelin-15',
    name: 'Tracelin 15 mg',
    generic: 'tracelinide mesylate',
    moiety: 'UNII: 2K8X1L4M7P',
    pharmClass: 'Tyrosine kinase inhibitor',
    armN: 180,
    comparatorN: 178,
    rowCount: 9,
    significant: 1,
    significantProtective: 0,
    placeboCoverage: false,
    activeCoverage: true,
    doseCoverage: 0.0,
    socBreadth: 4,
    socTotal: 17,
    monoComboMix: 'mono',
    score: 32,
    scoreReason: 'Low row count, no dose data, single significant signal',
  },
];

/* ============================================================
   AE DATA per drug — realistic patterns
   ============================================================ */
const SOC_SERIOUS = new Set([
  'Cardiac', 'Hepatobiliary', 'Renal & Urinary', 'Blood & Lymphatic',
  'Immune System', 'Vascular', 'Neoplasms',
]);

function aeRowsFor(drugId) {
  if (drugId === 'norvexis-25') return NORVEXIS_AES;
  if (drugId === 'olemitra-100') return OLEMITRA_AES;
  if (drugId === 'kavrolide-er') return KAVROLIDE_AES;
  return TRACELIN_AES;
}

const NORVEXIS_AES = [
  // Significant elevated, tight precision (Tier: counsel)
  { name: 'Headache', soc: 'Nervous System', nnh: 8, nnhL: 6, nnhH: 11, type: 'NNH', rr: 1.82, rrL: 1.44, rrH: 2.31, eT: 124, eC: 69, armN: 850, isPlac: true, prec: 'tight', sig: true },
  { name: 'Nausea', soc: 'Gastrointestinal', nnh: 12, nnhL: 9, nnhH: 17, type: 'NNH', rr: 1.61, rrL: 1.28, rrH: 2.03, eT: 98, eC: 54, armN: 850, isPlac: true, prec: 'tight', sig: true },
  { name: 'Dizziness', soc: 'Nervous System', nnh: 15, nnhL: 11, nnhH: 22, type: 'NNH', rr: 1.50, rrL: 1.20, rrH: 1.87, eT: 76, eC: 42, armN: 850, isPlac: true, prec: 'tight', sig: true },
  { name: 'Insomnia', soc: 'Psychiatric', nnh: 18, nnhL: 13, nnhH: 28, type: 'NNH', rr: 1.45, rrL: 1.15, rrH: 1.85, eT: 64, eC: 38, armN: 850, isPlac: true, prec: 'tight', sig: true },
  { name: 'Dry mouth', soc: 'Gastrointestinal', nnh: 22, nnhL: 16, nnhH: 35, type: 'NNH', rr: 1.40, rrL: 1.10, rrH: 1.80, eT: 55, eC: 32, armN: 850, isPlac: true, prec: 'tight', sig: true },
  { name: 'Decreased appetite', soc: 'Metabolism', nnh: 28, nnhL: 20, nnhH: 48, type: 'NNH', rr: 1.35, rrL: 1.08, rrH: 1.70, eT: 49, eC: 30, armN: 850, isPlac: true, prec: 'tight', sig: true },

  // Significant elevated, serious SOC, wide (Tier: watch)
  { name: 'QT prolongation', soc: 'Cardiac', nnh: 185, nnhL: 95, nnhH: 520, type: 'NNH', rr: 3.20, rrL: 1.60, rrH: 6.40, eT: 12, eC: 4, armN: 850, isPlac: true, prec: 'wide', sig: true },
  { name: 'Hepatic enzyme elevation', soc: 'Hepatobiliary', nnh: 240, nnhL: 120, nnhH: 780, type: 'NNH', rr: 2.80, rrL: 1.40, rrH: 5.80, eT: 8, eC: 3, armN: 850, isPlac: true, prec: 'wide', sig: true },
  { name: 'Suicidal ideation', soc: 'Psychiatric', nnh: 420, nnhL: 180, nnhH: 1900, type: 'NNH', rr: 2.40, rrL: 1.20, rrH: 5.00, eT: 5, eC: 2, armN: 850, isPlac: false, prec: 'wide', sig: true },
  { name: 'Seizure', soc: 'Nervous System', nnh: 560, nnhL: 220, nnhH: 2400, type: 'NNH', rr: 2.10, rrL: 1.10, rrH: 4.20, eT: 4, eC: 2, armN: 850, isPlac: true, prec: 'wide', sig: true },
  { name: 'Serotonin syndrome', soc: 'Nervous System', nnh: 780, nnhL: 290, nnhH: 4200, type: 'NNH', rr: 2.05, rrL: 1.05, rrH: 4.00, eT: 3, eC: 1, armN: 850, isPlac: false, prec: 'wide', sig: true },

  // Significant protective (Tier: reassure)
  { name: 'Constipation', soc: 'Gastrointestinal', nnt: 45, nntL: 33, nntH: 72, type: 'NNT', rr: 0.62, rrL: 0.43, rrH: 0.88, eT: 20, eC: 39, armN: 850, isPlac: true, prec: 'tight', sig: true, prot: true },

  // Not significant (Tier: reassure)
  { name: 'Rash', soc: 'Skin & Subcutaneous', nnh: null, type: 'NNH', rr: 1.12, rrL: 0.85, rrH: 1.48, eT: 42, eC: 38, armN: 850, isPlac: true, prec: 'tight', sig: false },
  { name: 'Fatigue', soc: 'General', nnh: null, type: 'NNH', rr: 1.08, rrL: 0.88, rrH: 1.32, eT: 88, eC: 82, armN: 850, isPlac: true, prec: 'tight', sig: false },
  { name: 'Diarrhea', soc: 'Gastrointestinal', nnh: null, type: 'NNH', rr: 0.95, rrL: 0.74, rrH: 1.22, eT: 54, eC: 57, armN: 850, isPlac: true, prec: 'tight', sig: false },
  { name: 'Back pain', soc: 'Musculoskeletal', nnh: null, type: 'NNH', rr: 1.03, rrL: 0.78, rrH: 1.36, eT: 36, eC: 35, armN: 850, isPlac: true, prec: 'tight', sig: false },
  { name: 'Cough', soc: 'Respiratory', nnh: null, type: 'NNH', rr: 1.06, rrL: 0.80, rrH: 1.40, eT: 30, eC: 28, armN: 850, isPlac: true, prec: 'tight', sig: false },
  { name: 'Upper respiratory infection', soc: 'Infections', nnh: null, type: 'NNH', rr: 0.98, rrL: 0.75, rrH: 1.28, eT: 48, eC: 49, armN: 850, isPlac: true, prec: 'tight', sig: false },

  // Fragile rows
  { name: 'Menstrual disorder', soc: 'Renal & Urinary', nnh: 25, nnhL: 7, nnhH: 99000, type: 'NNH', rr: 2.50, rrL: 0.80, rrH: 7.80, eT: 3, eC: 1, armN: 850, isPlac: true, prec: 'fragile', sig: false, flags: ['SOC_REMAP', 'WIDE_CI'] },
  { name: 'Hyperhidrosis', soc: 'Skin & Subcutaneous', nnh: null, type: 'NNH', rr: 0.0024, rrL: 0.00001, rrH: 0.45, eT: 0, eC: 8, armN: 850, isPlac: true, prec: 'fragile', sig: false, prot: true, flags: ['ZERO_CELL_CORRECTED'] },
  { name: 'Photosensitivity', soc: 'Skin & Subcutaneous', nnh: 60, nnhL: 18, nnhH: 14500, type: 'NNH', rr: 1.90, rrL: 0.70, rrH: 5.20, eT: 6, eC: 3, armN: 850, isPlac: false, prec: 'fragile', sig: false, flags: ['LOW_EVENT_COUNT'] },
];

const OLEMITRA_AES = [
  { name: 'Fatigue', soc: 'General', nnh: 14, nnhL: 9, nnhH: 28, type: 'NNH', rr: 1.55, rrL: 1.18, rrH: 2.03, eT: 62, eC: 40, armN: 412, isPlac: false, prec: 'tight', sig: true },
  { name: 'Headache', soc: 'Nervous System', nnh: 19, nnhL: 12, nnhH: 42, type: 'NNH', rr: 1.42, rrL: 1.08, rrH: 1.87, eT: 48, eC: 33, armN: 412, isPlac: false, prec: 'tight', sig: true, combo: true },
  { name: 'Nausea', soc: 'Gastrointestinal', nnh: 26, nnhL: 16, nnhH: 75, type: 'NNH', rr: 1.32, rrL: 1.02, rrH: 1.71, eT: 41, eC: 30, armN: 412, isPlac: false, prec: 'wide', sig: true, combo: true },
  { name: 'Anemia', soc: 'Blood & Lymphatic', nnh: 88, nnhL: 38, nnhH: 320, type: 'NNH', rr: 2.10, rrL: 1.15, rrH: 3.85, eT: 12, eC: 6, armN: 412, isPlac: false, prec: 'wide', sig: true },
  { name: 'ALT elevation', soc: 'Hepatobiliary', nnh: 145, nnhL: 60, nnhH: 920, type: 'NNH', rr: 2.40, rrL: 1.12, rrH: 5.10, eT: 7, eC: 3, armN: 412, isPlac: false, prec: 'wide', sig: true },
  { name: 'Insomnia', soc: 'Psychiatric', nnh: null, type: 'NNH', rr: 1.18, rrL: 0.88, rrH: 1.58, eT: 36, eC: 30, armN: 412, isPlac: false, prec: 'tight', sig: false, combo: true },
  { name: 'Pruritus', soc: 'Skin & Subcutaneous', nnh: null, type: 'NNH', rr: 1.05, rrL: 0.75, rrH: 1.46, eT: 22, eC: 21, armN: 412, isPlac: false, prec: 'tight', sig: false },
  { name: 'Diarrhea', soc: 'Gastrointestinal', nnh: null, type: 'NNH', rr: 0.92, rrL: 0.65, rrH: 1.30, eT: 28, eC: 30, armN: 412, isPlac: false, prec: 'tight', sig: false, combo: true },
  { name: 'Pancreatitis', soc: 'Gastrointestinal', nnh: 220, nnhL: 38, nnhH: 8200, type: 'NNH', rr: 3.00, rrL: 0.90, rrH: 9.80, eT: 4, eC: 1, armN: 412, isPlac: false, prec: 'fragile', sig: false, combo: true, flags: ['LOW_EVENT_COUNT', 'WIDE_CI'] },
];

const KAVROLIDE_AES = [
  { name: 'Peripheral edema', soc: 'General', nnh: 6, nnhL: 5, nnhH: 8, type: 'NNH', rr: 2.10, rrL: 1.78, rrH: 2.48, eT: 240, eC: 112, armN: 1240, isPlac: true, prec: 'tight', sig: true },
  { name: 'Headache', soc: 'Nervous System', nnh: 11, nnhL: 8, nnhH: 15, type: 'NNH', rr: 1.55, rrL: 1.30, rrH: 1.85, eT: 165, eC: 105, armN: 1240, isPlac: true, prec: 'tight', sig: true },
  { name: 'Flushing', soc: 'Vascular', nnh: 13, nnhL: 10, nnhH: 18, type: 'NNH', rr: 1.78, rrL: 1.42, rrH: 2.22, eT: 140, eC: 78, armN: 1240, isPlac: true, prec: 'tight', sig: true },
  { name: 'Dizziness', soc: 'Nervous System', nnh: 17, nnhL: 13, nnhH: 24, type: 'NNH', rr: 1.48, rrL: 1.20, rrH: 1.82, eT: 110, eC: 73, armN: 1240, isPlac: true, prec: 'tight', sig: true },
  { name: 'Fatigue', soc: 'General', nnh: 24, nnhL: 17, nnhH: 38, type: 'NNH', rr: 1.32, rrL: 1.08, rrH: 1.62, eT: 92, eC: 67, armN: 1240, isPlac: true, prec: 'tight', sig: true },
  { name: 'Gingival hyperplasia', soc: 'Gastrointestinal', nnh: 38, nnhL: 25, nnhH: 75, type: 'NNH', rr: 1.92, rrL: 1.25, rrH: 2.95, eT: 42, eC: 21, armN: 1240, isPlac: true, prec: 'tight', sig: true },

  // Watch
  { name: 'Hypotension', soc: 'Cardiac', nnh: 95, nnhL: 52, nnhH: 220, type: 'NNH', rr: 2.20, rrL: 1.32, rrH: 3.68, eT: 22, eC: 10, armN: 1240, isPlac: true, prec: 'wide', sig: true },
  { name: 'Reflex tachycardia', soc: 'Cardiac', nnh: 155, nnhL: 75, nnhH: 480, type: 'NNH', rr: 1.95, rrL: 1.15, rrH: 3.30, eT: 14, eC: 7, armN: 1240, isPlac: true, prec: 'wide', sig: true },
  { name: 'AV block', soc: 'Cardiac', nnh: 480, nnhL: 195, nnhH: 2100, type: 'NNH', rr: 2.60, rrL: 1.18, rrH: 5.70, eT: 6, eC: 2, armN: 1240, isPlac: true, prec: 'wide', sig: true },

  // Protective
  { name: 'Migraine', soc: 'Nervous System', nnt: 65, nntL: 42, nntH: 132, type: 'NNT', rr: 0.55, rrL: 0.35, rrH: 0.84, eT: 18, eC: 33, armN: 1240, isPlac: true, prec: 'tight', sig: true, prot: true },
  { name: 'Angina events', soc: 'Cardiac', nnt: 28, nntL: 19, nntH: 52, type: 'NNT', rr: 0.42, rrL: 0.26, rrH: 0.68, eT: 32, eC: 76, armN: 1240, isPlac: true, prec: 'tight', sig: true, prot: true },

  // NS
  { name: 'Nausea', soc: 'Gastrointestinal', nnh: null, type: 'NNH', rr: 1.10, rrL: 0.86, rrH: 1.40, eT: 78, eC: 71, armN: 1240, isPlac: true, prec: 'tight', sig: false },
  { name: 'Constipation', soc: 'Gastrointestinal', nnh: null, type: 'NNH', rr: 1.04, rrL: 0.80, rrH: 1.35, eT: 56, eC: 54, armN: 1240, isPlac: true, prec: 'tight', sig: false },
  { name: 'Rash', soc: 'Skin & Subcutaneous', nnh: null, type: 'NNH', rr: 0.96, rrL: 0.70, rrH: 1.32, eT: 38, eC: 40, armN: 1240, isPlac: true, prec: 'tight', sig: false },
];

const TRACELIN_AES = [
  { name: 'Diarrhea', soc: 'Gastrointestinal', nnh: 5, nnhL: 4, nnhH: 8, type: 'NNH', rr: 2.45, rrL: 1.85, rrH: 3.25, eT: 78, eC: 28, armN: 180, isPlac: false, prec: 'tight', sig: true },
  { name: 'Hand-foot syndrome', soc: 'Skin & Subcutaneous', nnh: 28, nnhL: 14, nnhH: 110, type: 'NNH', rr: 1.85, rrL: 1.10, rrH: 3.12, eT: 18, eC: 10, armN: 180, isPlac: false, prec: 'wide', sig: false },
  { name: 'Fatigue', soc: 'General', nnh: null, type: 'NNH', rr: 1.22, rrL: 0.85, rrH: 1.75, eT: 38, eC: 32, armN: 180, isPlac: false, prec: 'wide', sig: false },
  { name: 'Hypertension', soc: 'Vascular', nnh: 85, nnhL: 22, nnhH: 1200, type: 'NNH', rr: 2.50, rrL: 0.95, rrH: 6.55, eT: 9, eC: 4, armN: 180, isPlac: false, prec: 'fragile', sig: false, flags: ['LOW_EVENT_COUNT'] },
  { name: 'Anemia', soc: 'Blood & Lymphatic', nnh: 42, nnhL: 18, nnhH: 280, type: 'NNH', rr: 1.95, rrL: 1.05, rrH: 3.60, eT: 14, eC: 7, armN: 180, isPlac: false, prec: 'wide', sig: true },
  { name: 'Headache', soc: 'Nervous System', nnh: null, type: 'NNH', rr: 1.05, rrL: 0.70, rrH: 1.58, eT: 22, eC: 21, armN: 180, isPlac: false, prec: 'wide', sig: false },
  { name: 'Decreased platelets', soc: 'Blood & Lymphatic', nnh: 68, nnhL: 22, nnhH: 1400, type: 'NNH', rr: 2.15, rrL: 0.90, rrH: 5.20, eT: 8, eC: 4, armN: 180, isPlac: false, prec: 'fragile', sig: false, flags: ['LOW_EVENT_COUNT', 'WIDE_CI'] },
];

const FLAG_TEXT = {
  ZERO_CELL_CORRECTED: 'Zero events in one arm — Haldane 0.5 correction applied. Effect direction may be a calculation artifact, not a real effect.',
  SOC_REMAP: 'MedDRA System Organ Class was remapped by Stage 5 processing. Verify the SOC assignment against the source label.',
  WIDE_CI: 'Confidence interval spans more than two orders of magnitude. Treat the point estimate as a placeholder, not a real value.',
  LOW_EVENT_COUNT: 'Fewer than 10 total events. Estimate is unstable and could shift substantially with one or two additional cases.',
};

/* ============================================================
   TIER LOGIC
   ============================================================ */
function tierFor(ae) {
  if (ae.prec === 'fragile') return 'fragile';
  if (!ae.sig) return 'reassure';
  if (ae.prot) return 'reassure';
  // Elevated + significant
  if (ae.prec === 'tight' && (ae.nnh || 999) <= 50) return 'counsel';
  if (SOC_SERIOUS.has(ae.soc) || ae.nnh > 50) return 'watch';
  return 'counsel';
}

const TIERS = [
  { id: 'counsel',  name: 'Expect & counsel',         desc: 'Common, tight-precision signals — mention these to the patient up front.' },
  { id: 'watch',    name: 'Watch — rare but serious', desc: 'Lower-probability signals in serious organ systems. Brief, with red-flag instructions.' },
  { id: 'reassure', name: 'Reassure',                 desc: 'Not significantly elevated, or significantly protective. Use to counter common worries.' },
  { id: 'fragile',  name: 'Low confidence — interpret with care', desc: 'Data-quality flags or extreme bounds. Do not drive counseling from these alone.' },
];

/* ============================================================
   HELPERS
   ============================================================ */
const fmtN = n => n == null ? '—' : n >= 1000 ? Math.round(n).toLocaleString() : Math.round(n).toString();
const fmtRR = r => r == null ? '—' : r < 0.01 ? r.toExponential(1) : r.toFixed(2);
const fmt1 = r => r == null ? '—' : r.toFixed(1);

/* ============================================================
   ICONS
   ============================================================ */
const IconChevDown = (props) => (
  <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><polyline points="6 9 12 15 18 9"/></svg>
);
const IconDownload = (props) => (
  <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
);
const IconBookmark = (props) => (
  <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg>
);
const IconLogo = () => (
  <svg viewBox="0 0 95.11 71.96" width="22" height="17" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <path d="M0,8l.03,63.93h14.15l-.09-71.93h-6.09C3.58,0,0,3.58,0,8Z" fill="#e8c8a8"/>
    <polygon points="7.87 0 25.91 71.93 41.06 71.93 22.96 0 7.87 0" fill="#e8c8a8"/>
    <polygon points="29.39 0 47.43 71.93 62.58 71.93 44.49 0 29.39 0" fill="#e8c8a8"/>
    <path d="M51.22.03l18.04,71.93h4.89c5.21,0,9.03-4.9,7.76-9.95L66.31.03h-15.09Z" fill="#f4a126"/>
    <path d="M95.11,27.68h-15.07L73.1.03h8.85c3.67,0,6.87,2.5,7.76,6.06l5.4,21.6Z" fill="#f4a126"/>
  </svg>
);

/* ============================================================
   TOP BAR
   ============================================================ */
function TopBar() {
  return (
    <div className="topbar" data-screen-label="Topbar">
      <div className="topbar-inner">
        <a className="brand" href="#">
          <IconLogo />
          MedRecPro
        </a>
        <span className="brand-sep" />
        <span className="brand-sub">Adverse Events</span>
        <div className="topbar-spacer" />
        <button className="topbar-action" title="Save">
          <IconBookmark width="14" height="14" />
          <span>Save</span>
        </button>
        <button className="topbar-action" title="Export">
          <IconDownload width="14" height="14" />
          <span>Export</span>
        </button>
      </div>
    </div>
  );
}

/* ============================================================
   PAGE HEADER — drug selector + coverage
   ============================================================ */
function PageHeader({ drug, drugs, onPick }) {
  return (
    <div className="page-header">
      <div className="crumbs">
        Inventory <span>·</span> Per-product view <span>·</span> Adverse events
      </div>
      <div className="drug-selector">
        <DrugPicker drug={drug} catalog={drugs} onPick={onPick} />
        <div className="coverage-row">
          <span className="cov-badge is-on"><span className="cov-dot" /> Placebo-controlled {drug.placeboCoverage ? '✓' : ''}</span>
          {drug.activeCoverage
            ? <span className="cov-badge is-on"><span className="cov-dot" /> Active comparator ✓</span>
            : <span className="cov-badge is-off"><span className="cov-dot" /> No active comparator</span>}
          <span className={'cov-badge ' + (drug.doseCoverage > 0.3 ? 'is-on' : 'is-off')}>
            <span className="cov-dot" /> Dose data <span className="cov-num">{Math.round(drug.doseCoverage * 100)}%</span>
          </span>
          <span className="cov-badge is-on">
            <span className="cov-dot" /> SOC breadth <span className="cov-num">{drug.socBreadth}/{drug.socTotal}</span>
          </span>
        </div>
      </div>
    </div>
  );
}

/* ============================================================
   KPI STRIP
   ============================================================ */
function KpiStrip({ drug }) {
  const scoreSegs = 10;
  const filled = Math.round((drug.score / 100) * scoreSegs);
  return (
    <div className="kpi-strip">
      <div className="kpi-card">
        <div className="kpi-label">AE rows</div>
        <div className="kpi-value">{drug.rowCount}</div>
        <div className="kpi-sub">
          {drug.armN.toLocaleString()} pts · vs {drug.comparatorN.toLocaleString()} comparator
        </div>
      </div>
      <div className="kpi-card">
        <div className="kpi-label">Significant signals</div>
        <div className="kpi-value">{drug.significant}</div>
        <div className="kpi-sub">
          <span className="kpi-pip orange">{drug.significant - drug.significantProtective} elevated</span>
          <span className="kpi-pip teal">{drug.significantProtective} protective</span>
        </div>
      </div>
      <div className="kpi-card">
        <div className="kpi-label">Comparator mix</div>
        <div className="kpi-value">
          {drug.placeboCoverage && drug.activeCoverage ? 'Both' : drug.placeboCoverage ? 'Plcb' : 'Actv'}
          <span className="kpi-unit">strata</span>
        </div>
        <div className="kpi-sub">
          {drug.placeboCoverage && drug.activeCoverage
            ? 'Placebo + active comparator both present'
            : drug.placeboCoverage
              ? 'Placebo only — RR may overstate real-world risk'
              : 'Active-comparator only — no placebo stratum'}
        </div>
      </div>
      <div className="kpi-card">
        <div className="kpi-label">Chart-worthiness</div>
        <div className="kpi-value">{drug.score}<span className="kpi-unit">/100</span></div>
        <div className="score-bar">
          {Array.from({ length: scoreSegs }).map((_, i) =>
            <div key={i} className={'score-seg' + (i < filled ? ' on' : '')} />
          )}
        </div>
      </div>
    </div>
  );
}

/* ============================================================
   SYNTHETIC PRODUCT CATALOG
   ============================================================
   Simulates the ~450-item product list a real MedRecPro deployment
   would surface. The four hand-crafted entries above keep their full
   AE datasets; the rest are "ghost" entries that share AE templates
   so the dashboard always shows realistic numbers.
*/
const CATALOG_TEMPLATE_STEMS = [
  'nor','ole','kav','tra','pra','flu','rip','ven','dox','cef','mer','cita','ela','mox',
  'rab','sim','par','val','lor','ris','que','arip','gaba','lev','top','lam','est','los',
  'olm','aml','ator','rosu','met','bis','car','esci','sert','fluo','mira','tama','oxy',
  'hydro','aceta','napr','ibu','cefti','metro','clari','ery','azith','lina','sita',
  'empag','dapag','cana','dula','seme','lira','tirze','liraz','vortio','milnac',
];
const CATALOG_TEMPLATE_TAILS = [
  'vexis','mitra','rolide','celin','donax','staton','pridil','vanex','mavyr','centa',
  'sirib','tinox','phoresa','mirine','novix','olen','tavi','plexa','vrint','ruzin',
  'lestra','quan','xelor','derin','myne','tezor','prinox','vatra','melor','sirix',
];
const CATALOG_FORMS = ['mg','mg ER','mg IR','mg SR','mg XR','mg/mL'];
const CATALOG_DOSES = [2.5,5,10,12.5,15,20,25,40,50,75,100,150,200,250,300,400,500,600];
const CATALOG_SALTS = ['hydrochloride','mesylate','succinate','tartrate','fumarate','sulfate','tosylate','besylate','sodium','calcium'];
const CATALOG_CLASSES = [
  'SSRI','SNRI','TCA','Beta blocker (β1-selective)','ACE inhibitor','ARB',
  'Calcium channel blocker (L-type)','Thiazide diuretic','Loop diuretic',
  'Statin (HMG-CoA reductase)','PPI','H2 blocker',
  'Direct-acting antiviral (NS5A inhibitor)','NRTI','Integrase inhibitor',
  'Tyrosine kinase inhibitor','PARP inhibitor','CDK4/6 inhibitor','Anti-CD20 mAb',
  'Macrolide antibiotic','Fluoroquinolone','Cephalosporin (3rd gen)','Penicillin',
  'Anticonvulsant (sodium channel)','GABA-A modulator',
  'GLP-1 receptor agonist','SGLT2 inhibitor','DPP-4 inhibitor','Sulfonylurea',
  'Atypical antipsychotic','Mood stabilizer',
  'NSAID (non-selective)','COX-2 selective NSAID','μ-opioid agonist',
  'Bisphosphonate','Selective NE/5-HT modulator',
];

function buildSyntheticCatalog(targetN) {
  // Seeded LCG so the catalog is stable across reloads
  let seed = 1729;
  const rnd = () => { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed / 0x7fffffff; };
  const pick = (arr) => arr[Math.floor(rnd() * arr.length)];
  const cap = (s) => s.charAt(0).toUpperCase() + s.slice(1);
  const uniiChars = '0123456789ABCDEFGHJKMNPRSTVWXYZ';

  const used = new Set(DRUGS.map(d => d.id));
  const out = [];
  let guard = 0;
  while (out.length < targetN && guard++ < targetN * 8) {
    const stem = pick(CATALOG_TEMPLATE_STEMS);
    const tail = pick(CATALOG_TEMPLATE_TAILS);
    const brand = cap(stem + tail);
    const dose = pick(CATALOG_DOSES);
    const form = pick(CATALOG_FORMS);
    const id = (brand + '-' + dose + '-' + form).toLowerCase().replace(/[^a-z0-9-]/g, '');
    if (used.has(id)) continue;
    used.add(id);
    const generic = (stem + tail).toLowerCase() + ' ' + pick(CATALOG_SALTS);
    const cls = pick(CATALOG_CLASSES);
    const moiety = 'UNII: ' + Array.from({ length: 10 }, () => uniiChars[Math.floor(rnd() * uniiChars.length)]).join('');
    const score = Math.round(18 + rnd() * 78);
    const rowCount = Math.round(4 + rnd() * 82);
    out.push({
      id,
      name: `${brand} ${dose} ${form}`,
      generic,
      pharmClass: cls,
      moiety,
      score,
      rowCount,
      _ghost: true,
    });
  }
  return out;
}

const SYNTHETIC_CATALOG = buildSyntheticCatalog(450 - DRUGS.length);
const CATALOG = [...DRUGS, ...SYNTHETIC_CATALOG];

/**
 * Look up a drug by id and return a fully-populated record suitable for
 * the KPI strip + page header. Ghost entries are hydrated from one of the
 * four real drug templates so the dashboard always has data to show.
 */
function expandDrug(id) {
  const real = DRUGS.find(d => d.id === id);
  if (real) return real;
  const ghost = CATALOG.find(d => d.id === id);
  if (!ghost) return DRUGS[0];
  // Hash-pick a template so each ghost stably maps to the same AE dataset
  let h = 0;
  for (let i = 0; i < id.length; i++) h = ((h * 31) + id.charCodeAt(i)) >>> 0;
  const tpl = DRUGS[h % DRUGS.length];
  return {
    ...tpl,
    id: ghost.id,
    name: ghost.name,
    generic: ghost.generic,
    pharmClass: ghost.pharmClass,
    moiety: ghost.moiety,
    score: ghost.score,
    rowCount: ghost.rowCount,
    scoreReason: `Template — backed by ${tpl.name.split(' ')[0]} AE pattern`,
    _ghost: true,
    _templateId: tpl.id,
  };
}

function aeRowsForExpanded(id) {
  const exp = expandDrug(id);
  return aeRowsFor(exp._templateId || exp.id);
}

Object.assign(window, {
  TopBar, PageHeader, KpiStrip,
  DRUGS, CATALOG, expandDrug, aeRowsForExpanded,
  NORVEXIS_AES, OLEMITRA_AES, KAVROLIDE_AES, TRACELIN_AES,
  aeRowsFor, tierFor, TIERS, fmtN, fmtRR, fmt1, FLAG_TEXT, SOC_SERIOUS,
});
