# MedRecPro Prototypes

Front-end UI prototypes for **MedRecPro** — exploratory, self-contained design
mockups that visualize parsed pharmaceutical data. These are *prototypes*, not
production code: there is no build step, no package manager, and no backend. The
data is synthetic and the prototypes are intended for design review and
interaction testing only.

> ⚠️ **Not for clinical use.** Every drug, statistic, and adverse-event figure in
> these prototypes is fictional or synthetic.

## Contents

| Prototype | Description |
|---|---|
| [`AE Dashboard 01`](./AE%20Dashboard%2001) | Adverse-event (AE) risk dashboard — per-product triage, forest plot, risk-vs-precision quadrant, plus cross-product reverse lookup and therapeutic-interchange tools. |

---

## AE Dashboard 01

A browser-based React prototype for the MedRecPro adverse-events visualization
layer. It presents the kind of data that a real deployment would surface from the
`tmp_FlattenedAdverseEventRiskTable` projection: relative risk, confidence
intervals, number-needed-to-harm/treat, and data-quality flags for each AE row of
a product.

### Primary views (per-product)

The main panel offers three tabs over the selected product's AE rows:

- **Triage** *(flagship)* — AEs sorted into four action tiers:
  - **Expect & counsel** — common, tight-precision signals to mention up front.
  - **Watch — rare but serious** — low-probability signals in serious organ
    systems, with red-flag guidance.
  - **Reassure** — not significantly elevated, or significantly protective.
  - **Low confidence — interpret with care** — rows with data-quality flags or
    extreme bounds. These render desaturated and never enter the "Expect &
    counsel" tier.
  Each row expands to show treatment/comparator event counts, risk type, and the
  specific reasons a row is flagged low-confidence.
- **Forest** — a forest plot of relative risk (RR) with 95% confidence intervals
  on a log scale (0.1–10), with an RR = 1 reference line. Elevated, protective,
  and non-significant signals are color-coded.
- **Quadrant** — a risk-vs-precision scatter plot: effect magnitude on the
  y-axis, estimate precision (CI width) on the x-axis, bubble size ∝ √(events).
  The four quadrants map to *Investigate / Warn / Ignore / Reassure*.

A comparator filter (All / Placebo / Active comparator) and a "show fragile rows"
toggle apply across all three views.

### Cross-product tools

- **Symptom → drug reverse lookup** — start from a patient complaint and rank the
  regimen's drugs by how plausibly each one explains it. Surfaces a reassurance
  banner when no drug shows a significantly elevated rate.
- **Therapeutic interchange differential** — compare two products side by side and
  see which adverse events get *worse*, *better*, *stay the same*, or appear
  *uniquely* on one. Warns when the products differ in pharmacologic class or do
  not share comparable comparator strata.

### Supporting UI

- **Drug picker** — a typeahead combobox over a ~450-product catalog, with
  recents and favorites persisted in `localStorage` and full keyboard navigation.
  Two variants: a large header trigger and a compact inline trigger (used by the
  interchange tool, with cross-picker exclusivity).
- **KPI strip** — AE row count, significant-signal counts (elevated vs.
  protective), comparator mix, and a "chart-worthiness" score out of 100.
- **Coverage badges** — placebo-controlled, active-comparator, dose-data
  percentage, and System Organ Class (SOC) breadth.
- **Tweaks panel** — a floating design-control panel that communicates with an
  external editing host via `postMessage`. It currently exposes a single tweak
  (horizontal nudge of the quadrant's rotated axis label).

### Data model

The synthetic AE rows carry the statistics a real product page would show:

- **NNH / NNT** — number needed to harm / treat, with bounds.
- **RR** — relative risk with a 95% confidence interval.
- **Precision class** — `tight`, `wide`, or `fragile`.
- **Significance** and a **protective** flag.
- **MedDRA System Organ Class (SOC)**, with a "serious SOC" set.
- **Data-quality flags** — e.g. `ZERO_CELL_CORRECTED` (Haldane 0.5 correction),
  `SOC_REMAP`, `WIDE_CI`, `LOW_EVENT_COUNT`.

Four products (Norvexis, Olemitra, Kavrolide ER, Tracelin) are hand-authored with
full AE datasets. A seeded linear-congruential generator builds ~450 additional
"ghost" catalog entries — stable across reloads — that hydrate from one of the
four real templates, so the picker behaves like a populated deployment.

---

## File layout

```
AE Dashboard 01/
├── dashboard.jsx        Core data (drug catalog, AE datasets), KPI strip,
│                        page header, tier logic, formatting helpers
├── drug-picker.jsx      Typeahead drug picker (recents/favorites, keyboard nav)
├── extras.jsx           Reverse-lookup and therapeutic-interchange panels
├── app.jsx              Triage / Forest / Quadrant views + root <App>
├── tweaks-panel.jsx     Reusable floating "Tweaks" control shell + host protocol
├── tweaks.jsx           Tweak definitions for this prototype
└── medrecpro-site.css   Design tokens and all component styles (~2,100 lines)
```

There is **no `index.html` checked in** — these files are exported from a
prototyping host that injects React, ReactDOM, Babel, and the page shell. The
modules share components by assigning them onto the global `window` object (no
bundler / ES modules), so **script load order matters** and `app.jsx` must run
last.

## Running it locally

To run the dashboard outside its original host, add a small HTML page that loads
React, ReactDOM, and Babel standalone, then the source files in dependency order.
Save this as `index.html` inside `AE Dashboard 01/`:

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>MedRecPro · AE Dashboard 01</title>
  <link rel="stylesheet" href="medrecpro-site.css" />
  <script crossorigin src="https://unpkg.com/react@18/umd/react.development.js"></script>
  <script crossorigin src="https://unpkg.com/react-dom@18/umd/react-dom.development.js"></script>
  <script src="https://unpkg.com/@babel/standalone/babel.min.js"></script>
</head>
<body>
  <div id="root"></div>
  <!-- Load order matters: components register on window; app.jsx renders last. -->
  <script type="text/babel" src="dashboard.jsx"></script>
  <script type="text/babel" src="drug-picker.jsx"></script>
  <script type="text/babel" src="extras.jsx"></script>
  <script type="text/babel" src="tweaks-panel.jsx"></script>
  <script type="text/babel" src="tweaks.jsx"></script>
  <script type="text/babel" src="app.jsx"></script>
</body>
</html>
```

Because Babel fetches the `.jsx` files over HTTP, serve the folder from a local
web server rather than opening the file directly:

```bash
# from inside "AE Dashboard 01/"
npx serve            # or: python -m http.server 8000
```

Then open the served URL in a browser.

## Tech stack

- **React 18** + **ReactDOM**, with JSX transformed in the browser by
  **Babel standalone** — no build pipeline.
- Plain CSS with custom-property design tokens (burnt-orange / dark-brown /
  earth-tone palette).
- `localStorage` for picker recents and favorites.

## Status

Design prototype. Synthetic data only. Not for clinical use.
