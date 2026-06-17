# MedRecProReact

The **Adverse Event Risk Dashboard** for MedRecPro — a React + Vite single-page
"island" that visualizes the pre-computed adverse-event risk statistics produced
by the MedRecPro table-standardization pipeline (Stage 5 AE denormalization).

The app runs in two modes from the same source:

- **Standalone Vite dev server** for fast local UI iteration (`npm run dev`).
- **MVC-hosted island** at `/adverse-events`, served by `MedRecProStatic` from a
  committed Vite bundle.

It reads exclusively from the live `/api/AdverseEvent` surface
(`AdverseEventController` in the MedRecPro API); it ships no synthetic data.

## Technology Stack

| Concern | Choice |
|---|---|
| UI library | React 19 |
| Build tool / dev server | Vite 8 (`@vitejs/plugin-react`) |
| Unit tests | Vitest 4 |
| Linting | ESLint 10 (flat config, `eslint.config.js`) with `react-hooks` + `react-refresh` plugins |
| Language | Plain JavaScript / JSX (no TypeScript; `@types/*` are dev-only for editor hints) |
| Styling | Hand-authored CSS (`src/index.css`); the masthead is **not** styled here (see below) |

There are no runtime dependencies beyond `react` / `react-dom`. Charts (forest
plot, quadrant, correlation map, heatmap) are rendered with plain DOM + CSS and
small in-house scale helpers rather than a charting library.

## How It Is Hosted

```
  MedRecProReact (source)                 MedRecProStatic (host)
  ┌──────────────────────┐    npm run     ┌──────────────────────────────────┐
  │ src/  index.html     │ ─── build ───► │ wwwroot/ae-dashboard/            │
  │ vite.config.js       │                │   ae-dashboard.js                │
  └──────────────────────┘                │   ae-dashboard.css               │
                                          │   index.html / favicon.svg ...   │
                                          │ Views/AdverseEventDashboard/     │
                                          │   Index.cshtml  → /adverse-events│
                                          └──────────────────────────────────┘
```

`vite.config.js` pins three deployment facts:

- `base: '/ae-dashboard/'` — the public path the MVC view loads assets from.
- `build.outDir: '../MedRecProStatic/wwwroot/ae-dashboard'` — the build writes
  **directly into the static site's web root** (`emptyOutDir: true`).
- Deterministic filenames: `ae-dashboard.js` and `ae-dashboard.css`, so the
  Razor host view (`Views/AdverseEventDashboard/Index.cshtml`) can reference them
  by a stable name.

The app mounts on `#aeDashboardApp` when MVC-hosted, falling back to `#root` on
the standalone Vite page (`src/main.jsx`).

> **Important:** `dotnet build` does **not** run Vite. After changing any React
> source you must rebuild and commit the bundle:
>
> ```powershell
> npm.cmd --prefix ..\MedRecProReact run build
> ```
>
> Commit the regenerated `MedRecProStatic/wwwroot/ae-dashboard/*` assets together
> with the React source change.

## Masthead

The MedRecPro masthead (logo, primary nav, subtitle, hamburger toggle, and the
Save/Export action buttons) is **server-rendered once** by the shared
`MedRecProStatic/Views/Shared/_Masthead.cshtml` partial and styled by
`masthead.css` — a single source of truth across every server-rendered page and
this island. React does not render or style the masthead; it only wires the
behavior of the host-injected Save/Export buttons by id (`#aeSaveBtn` /
`#aeExportBtn`) when MVC-hosted. The standalone Vite dev page has no masthead
because it does not render the Razor partial.

## Dashboard Surface

The dashboard has two top-level **focuses**, selected with the focus switch and
reflected in the URL (`?focus=product|class`).

### Product focus (`focus=product`)

A single drug product is selected with the live product picker (server search,
favorites, and local recents). Its adverse-event signals are shown three ways:

| View | Description |
|---|---|
| **Triage** | AE signals grouped into action tiers (counsel / watch / reassure / fragile), each row showing RR with 95% CI, NNH/NNT, SOC tag, dose, and an expandable evidence detail. |
| **Forest plot** | Relative risk with confidence intervals on a shared dynamic log scale; protective ↔ RR=1 ↔ elevated. |
| **Quadrant** | Effect magnitude (y) vs. estimate precision (x), with bubble size tracking event volume. |

Two cross-product tools sit beneath the views:

- **Symptom reverse lookup** — find which scoped products report one or more exact
  AE terms, with a causal/protective/low-confidence verdict per match.
- **Therapeutic interchange** — compare two products side by side as paired RR
  mini-tracks, grouped by which product carries the higher-concern signal, with
  `Differences only` and `Shared signals only` toggles.

### Class focus (`focus=class`)

A pharmacologic class is selected from the class picker (which surfaces how many
classes are map-ready vs. heatmap-only). Within a class:

| View | Description |
|---|---|
| **Correlation map** | SOC × SOC correlation of per-drug LogRR within the class (Spearman default, Pearson opt-in; median/mean LogRR aggregation). |
| **Heatmap** | Sparse SOC × drug RR grid — the honest companion that shows the underlying per-drug data behind each correlation cell. |
| **Cell drill-down** | Per-drug pairs behind a selected correlation cell, with map-safe vs. raw diagnostic coefficients. |

The class views are deliberately **honesty-first**: the observation unit is a
drug within the class, so each correlation cell's sample size is small. Cells
below the `minDrugsPerCell` floor (clamped to a minimum of 3) return as
suppressed `null` coefficients rather than fabricated numbers, and every payload
carries `Warnings` (mixed comparator, fragile included, low n, non-PSD pairwise
deletion).

### Shared controls

- **Comparator filter** — `all` / `placebo` / `active` (product focus);
  `Placebo` / `Active` / `Both` (class focus).
- **Fragile toggle** — show or hide low-precision rows.
- **URL state** — focus, product, class, active view, comparator, and class
  filters are all bookmarkable query parameters.

### Data limitation caveat

The dashboard footnote states the displayed figures do **not** represent every
product-attributable adverse outcome: coverage is bounded by what each product's
label discloses and by what the SPL table parser can extract. Absence of a
signal is not evidence of its absence in practice.

## Project Structure

```
MedRecProReact/
  index.html                     # Vite entry; mounts #aeDashboardApp (fallback #root)
  vite.config.js                 # base path + build into MedRecProStatic web root
  eslint.config.js               # ESLint flat config
  package.json                   # scripts + deps
  medrecproreact.esproj          # VS solution project wrapper for the JS project
  public/
    favicon.svg
    icons.svg
  src/
    main.jsx                     # React root; imports index.css only (no masthead)
    App.jsx                      # Dashboard shell: focus/view routing, state, all panels
    index.css                    # Dashboard body styles (masthead excluded)
    api/
      apiConfig.js               # Resolves the /api/AdverseEvent base for dev vs. prod
      adverseEventClient.js      # Typed wrapper over every AdverseEventController endpoint
      apiError.js                # ApiError + error-payload reader
    lib/
      normalizers.js             # API DTO → client view-model normalization (Pascal/camel safe)
      forestScale.js             # Shared dynamic log-scale ticks/positions for forest tracks
      correlationScales.js       # Diverging color scale for correlation / LogRR
      formatters.js              # Decimal/integer/dose formatting
      storage.js                 # localStorage helpers (recents)
    hooks/
      useProducts.js             # Product catalog + search + paging
      useFavorites.js            # Authenticated favorite add/remove/list
      useRecents.js              # Local recents
      useDebouncedValue.js
      useMediaQuery.js           # Responsive tick thinning
    components/
      PageHeader.jsx  KpiStrip.jsx  ProductPicker.jsx  FocusSwitch.jsx
      ClassPicker.jsx  ClassPageHeader.jsx  ClassKpiStrip.jsx  classPickerHelpers.js
      common/         # DisabledFeature, EmptyState, InlineError, Loading
      correlation/    # ClassCorrelationSurface, CorrelationMap, CorrelationHeatmap,
                      #   CorrelationCellDetail, CorrelationTooltip, socLabels, correlationMapCells
    test/             # Vitest specs for client URLs, normalizers, formatters, scales, pickers
```

## API Contract

All requests go to `AdverseEventController` under `/api/AdverseEvent` with
`credentials: 'include'`. `src/api/apiConfig.js` resolves the base URL per host:

| Host | API base |
|---|---|
| Vite dev server (port `50346`, HTTP) | `http://localhost:5093/api/AdverseEvent` |
| Local static host (HTTP, ports 5001/7199/30582/44318) | `http://localhost:5093/api/AdverseEvent` |
| Local static host (HTTPS) | `https://localhost:7201/api/AdverseEvent` |
| Production / anything else | same-origin `/api/AdverseEvent` |

Endpoints consumed (see the [MedRecPro README](../README.md) for the full table):
`products`, `products/catalog`, `products/count`, `products/favorites`,
`PUT|DELETE products/{guid}/favorite`, `products/{guid}/{triage,forest,quadrant}`,
`reverse-lookup`, `interchange`, and `correlation[/classes|/heatmap|/cell]`. The
class picker reads pagination/aggregate totals from the `X-Page-Number`,
`X-Page-Size`, `X-Total-Count`, and `X-Chartable-Count` response headers.

The whole feature is gated server-side by `FeatureFlags:AeDashboard:Enabled`;
when disabled the client renders the `DisabledFeature` state.

## Scripts

| Command | Action |
|---|---|
| `npm run dev` | Start the Vite dev server at `http://localhost:50346/ae-dashboard/`. |
| `npm run build` | Build the committed bundle into `../MedRecProStatic/wwwroot/ae-dashboard`. |
| `npm run lint` | Run ESLint over the project. |
| `npm run test` | Run the Vitest suite once (`vitest run`). |
| `npm run preview` | Preview a production build locally. |

## Testing

Unit tests live under `src/test/` and run with Vitest (`npm run test`). They
cover the deterministic, browser-free logic — API URL serialization, DTO
normalization (including numeric vs. string enum payloads and Pascal/camel
casing), formatters, forest/correlation scales, and the class-picker/correlation
helpers — rather than full component rendering.

## Related Projects

- **[MedRecPro](../MedRecPro)** — hosts `AdverseEventController` (`/api/AdverseEvent`) and the `vw_Ae*` views this dashboard reads.
- **[MedRecProStatic](../MedRecProStatic)** — hosts the committed bundle at `/adverse-events` and owns the shared masthead.
- **[MedRecProImportClass](../MedRecProImportClass)** — produces the Stage 5 AE denormalization / risk tables (`tmp_FlattenedAdverseEventRiskTable`, etc.) that ultimately feed this dashboard. See its README for the RR/DNRR statistical contract.
- **[MedRecProPrototypes](../MedRecProPrototypes)** — the standalone HTML/JS prototype this island was built to match.
