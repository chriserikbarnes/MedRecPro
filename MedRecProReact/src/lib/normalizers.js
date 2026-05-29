/**************************************************************/
/**
 * Reads the first populated value from a DTO, tolerating .NET acronym casing.
 *
 * @param {Record<string, unknown> | null | undefined} source - DTO to inspect.
 * @param {string[]} keys - Candidate property names.
 * @returns {unknown} First non-null value.
 */
function readFirst(source, keys) {
  // Missing DTOs normalize to undefined so callers can provide their own fallback.
  if (!source) {
    return undefined;
  }

  // Each key is tried in order because server and JSON naming policies can differ.
  for (const key of keys) {
    // A property is accepted only when it exists and is not null or undefined.
    if (source[key] !== undefined && source[key] !== null) {
      return source[key];
    }
  }

  return undefined;
}

/**************************************************************/
/**
 * Converts nullable API values into display strings.
 *
 * @param {unknown} value - Value to stringify.
 * @param {string} fallback - Value used when the source is empty.
 * @returns {string} Normalized display string.
 */
function toDisplayString(value, fallback = '') {
  // Nullish values use the supplied fallback.
  if (value === null || value === undefined) {
    return fallback;
  }

  // Trim strings so whitespace-only server values do not render as real labels.
  if (typeof value === 'string') {
    const trimmedValue = value.trim();
    return trimmedValue.length > 0 ? trimmedValue : fallback;
  }

  return String(value);
}

/**************************************************************/
/**
 * Converts API numerics while preserving null when no real number exists.
 *
 * @param {unknown} value - Candidate numeric value.
 * @returns {number | null} Parsed number or null.
 */
function toNullableNumber(value) {
  // Nullish values remain null so formatters can render a dash.
  if (value === null || value === undefined || value === '') {
    return null;
  }

  // Numbers are accepted only when finite.
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }

  // Strings can appear when JSON is generated from database decimals.
  const parsedValue = Number(value);

  return Number.isFinite(parsedValue) ? parsedValue : null;
}

/**************************************************************/
/**
 * Converts API booleans from bools, nullable bools, or common string values.
 *
 * @param {unknown} value - Candidate boolean value.
 * @returns {boolean} Normalized boolean.
 */
function toBoolean(value) {
  // Native booleans are already normalized.
  if (typeof value === 'boolean') {
    return value;
  }

  // String values are normalized case-insensitively.
  if (typeof value === 'string') {
    return value.toLowerCase() === 'true';
  }

  return Boolean(value);
}

/**************************************************************/
/**
 * Normalizes enum-ish strings to stable lowercase UI tokens.
 *
 * @param {unknown} value - Candidate enum value.
 * @param {string} fallback - Fallback token.
 * @returns {string} Lowercase token.
 */
function toToken(value, fallback = '') {
  // Empty source values use the fallback token.
  if (value === null || value === undefined || value === '') {
    return fallback;
  }

  return String(value).trim().toLowerCase();
}

/**************************************************************/
/**
 * Splits semicolon or comma delimited calculation flags into a stable list.
 *
 * @param {unknown} flags - API flags value.
 * @returns {string[]} Clean flag list.
 */
function normalizeFlags(flags) {
  // The prototype only treats true data-quality flags as low-confidence row notes.
  const lowConfidenceFlags = new Set([
    'ZERO_CELL_CORRECTED',
    'SocRemap',
    'SOC_REMAP',
    'WideCi',
    'WIDE_CI',
    'LowEventCount',
    'LOW_EVENT_COUNT',
  ]);

  // Array flags are accepted if a later API returns a typed collection.
  const candidateFlags = Array.isArray(flags)
    ? flags.map((flag) => toDisplayString(flag)).filter(Boolean)
    : typeof flags === 'string'
      ? flags
          .split(/[;,|]/)
          .map((flag) => flag.trim())
          .filter(Boolean)
      : [];

  // Comparator and standardization provenance flags are intentionally omitted
  // from the collapsed clinical row because the prototype reserves this slot
  // for low-confidence interpretation warnings only.
  return candidateFlags.filter((flag) => lowConfidenceFlags.has(flag));
}

/**************************************************************/
/**
 * Converts ASP.NET enum JSON values into prototype precision tokens.
 *
 * @param {unknown} value - PrecisionClass value from the API.
 * @returns {string} Prototype precision token.
 */
function normalizePrecisionClass(value) {
  // Numeric enum values follow AePrecisionClass: Tight=0, Wide=1, Fragile=2.
  if (value === 0 || value === '0') {
    return 'tight';
  }

  // Wide rows have broader intervals but are still signal-bearing.
  if (value === 1 || value === '1') {
    return 'wide';
  }

  // Fragile rows are low-confidence and may be hidden by the user.
  if (value === 2 || value === '2') {
    return 'fragile';
  }

  // String enum values are normalized case-insensitively.
  const token = toToken(value, 'unknown');

  // Only the prototype classes should pass through to CSS.
  if (token === 'tight' || token === 'wide' || token === 'fragile') {
    return token;
  }

  return 'unknown';
}

/**************************************************************/
/**
 * Converts ASP.NET enum JSON values into NNH/NNT tokens.
 *
 * @param {unknown} value - NumberNeededKind or NumberNeededType value.
 * @returns {string} Prototype number-needed token.
 */
function normalizeNumberNeededKind(value) {
  // Numeric enum values follow AeNumberNeededType: None=0, NNH=1, NNT=2.
  if (value === 1 || value === '1') {
    return 'NNH';
  }

  // NNT marks protective benefit rows.
  if (value === 2 || value === '2') {
    return 'NNT';
  }

  // String values from the SQL view are already close to the desired display.
  const token = toDisplayString(value).toUpperCase();

  // Only known prototype values are accepted.
  if (token === 'NNH' || token === 'NNT') {
    return token;
  }

  return '';
}

/**************************************************************/
/**
 * Converts server risk-significance text into a stable chart direction token.
 *
 * @param {unknown} value - Server risk-significance value.
 * @returns {string} Chart direction token.
 */
function normalizeDirection(value) {
  // Numeric enum values follow AeRiskSignificance: NotSignificant=0, Elevated=1, Protective=2.
  if (value === 1 || value === '1') {
    return 'elevated';
  }

  // Protective rows plot left/teal in the prototype.
  if (value === 2 || value === '2') {
    return 'protective';
  }

  // Explicit zero remains neutral.
  if (value === 0 || value === '0') {
    return 'ns';
  }

  // Normalized text lets enum strings and display strings share one path.
  const token = toToken(value, 'not significant');

  // Elevated rows use the prototype's orange risk direction.
  if (token === 'elevated') {
    return 'elevated';
  }

  // Protective rows use the prototype's teal benefit direction.
  if (token === 'protective') {
    return 'protective';
  }

  // All other values render as neutral/not-significant.
  return 'ns';
}

/**************************************************************/
/**
 * Normalizes an AeDrugSummaryDto into the React product view model.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API product DTO.
 * @returns {object | null} Product view model or null.
 */
export function normalizeProduct(dto) {
  // Missing products are represented as null rather than a partial object.
  if (!dto) {
    return null;
  }

  // DocumentGUID can arrive as DocumentGUID, documentGUID, or documentGuid.
  const documentGuid = toDisplayString(
    readFirst(dto, ['DocumentGUID', 'documentGUID', 'documentGuid']),
  );

  // Products without document GUIDs cannot participate in dashboard navigation.
  if (!documentGuid) {
    return null;
  }

  // Pharmacologic class display falls back to the class code when needed.
  const pharmClassName = toDisplayString(
    readFirst(dto, ['PharmClassName', 'pharmClassName']),
    toDisplayString(readFirst(dto, ['PharmClassCode', 'pharmClassCode']), 'Unclassified'),
  );

  return {
    id: documentGuid,
    documentGuid,
    name: toDisplayString(readFirst(dto, ['ProductName', 'productName']), 'Unnamed product'),
    generic: toDisplayString(readFirst(dto, ['SubstanceName', 'substanceName']), 'Substance not listed'),
    moiety: toDisplayString(readFirst(dto, ['UNII', 'unii']), 'UNII unavailable'),
    pharmClass: pharmClassName,
    armN: toNullableNumber(readFirst(dto, ['ArmN', 'armN'])),
    comparatorN: toNullableNumber(readFirst(dto, ['ComparatorN', 'comparatorN'])),
    rowCount: toNullableNumber(readFirst(dto, ['RowCount', 'rowCount'])) ?? 0,
    significant: toNullableNumber(readFirst(dto, ['SignificantCount', 'significantCount'])) ?? 0,
    significantProtective:
      toNullableNumber(readFirst(dto, ['SignificantProtectiveCount', 'significantProtectiveCount'])) ?? 0,
    significantElevated:
      toNullableNumber(readFirst(dto, ['SignificantElevatedCount', 'significantElevatedCount'])) ?? 0,
    placeboCoverage: toBoolean(readFirst(dto, ['PlaceboCoverage', 'placeboCoverage'])),
    activeCoverage: toBoolean(readFirst(dto, ['ActiveCoverage', 'activeCoverage'])),
    doseCoverage: toNullableNumber(readFirst(dto, ['DoseCoverage', 'doseCoverage'])) ?? 0,
    socBreadth: toNullableNumber(readFirst(dto, ['SocBreadth', 'socBreadth'])) ?? 0,
    socTotal: toNullableNumber(readFirst(dto, ['SocTotal', 'socTotal'])) ?? 0,
    monoComboMix: toDisplayString(readFirst(dto, ['MonoComboMix', 'monoComboMix']), 'Mixed'),
    score: toNullableNumber(readFirst(dto, ['Score', 'score'])),
    scoreReason: toDisplayString(readFirst(dto, ['ScoreReason', 'scoreReason']), 'Score pending'),
    isFavorite: toBoolean(readFirst(dto, ['IsFavorite', 'isFavorite'])),
  };
}

/**************************************************************/
/**
 * Normalizes a product collection and drops invalid rows.
 *
 * @param {unknown} payload - API product payload.
 * @returns {object[]} Product view models.
 */
export function normalizeProducts(payload) {
  // The controller returns arrays; defensive fallback keeps render paths stable.
  const items = Array.isArray(payload) ? payload : [];

  return items.map((item) => normalizeProduct(item)).filter(Boolean);
}

/**************************************************************/
/**
 * Normalizes one AE risk signal for suggestion and later visualization reuse.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API signal DTO.
 * @returns {object | null} Signal view model or null.
 */
export function normalizeSignal(dto) {
  // Missing signals are represented as null.
  if (!dto) {
    return null;
  }

  // The encrypted risk-table ID is the stable client-side signal key.
  const encryptedRiskId = toDisplayString(
    readFirst(dto, [
      'EncryptedFlattenedAdverseEventRiskTableID',
      'encryptedFlattenedAdverseEventRiskTableID',
    ]),
  );

  // Number-needed kind has existed under both Kind and Type names.
  const numberNeededKind = normalizeNumberNeededKind(
    readFirst(dto, ['NumberNeededKind', 'numberNeededKind', 'NumberNeededType', 'numberNeededType']),
  );

  // Keep the raw number-needed values, then expose only the matching NNH or NNT side.
  const numberNeeded = toNullableNumber(readFirst(dto, ['NumberNeeded', 'numberNeeded']));
  const numberNeededLower = toNullableNumber(
    readFirst(dto, ['NumberNeededLowerBound', 'numberNeededLowerBound']),
  );
  const numberNeededUpper = toNullableNumber(
    readFirst(dto, ['NumberNeededUpperBound', 'numberNeededUpperBound']),
  );

  // The NNH/NNT split is display-only and does not rederive server significance.
  const isNnt = numberNeededKind === 'NNT';

  // Risk significance may arrive as an enum string or persisted display text.
  const riskSignificance = toDisplayString(
    readFirst(dto, ['RiskSignificance', 'riskSignificance', 'Significance', 'significance']),
    'not significant',
  );

  // The server usually sends IsProtective, but significance is a safe fallback.
  const isProtective = toBoolean(readFirst(dto, ['IsProtective', 'isProtective']))
    || normalizeDirection(riskSignificance) === 'protective';

  // The server usually sends IsSignificant, but significance is a safe fallback.
  const isSignificant = toBoolean(readFirst(dto, ['IsSignificant', 'isSignificant']))
    || normalizeDirection(riskSignificance) === 'elevated'
    || normalizeDirection(riskSignificance) === 'protective';

  return {
    id: encryptedRiskId || toDisplayString(readFirst(dto, ['ParameterName', 'parameterName'])),
    name: toDisplayString(readFirst(dto, ['ParameterName', 'parameterName']), 'Unnamed signal'),
    soc: toDisplayString(readFirst(dto, ['ParameterCategory', 'parameterCategory']), 'Uncategorized'),
    rr: toNullableNumber(readFirst(dto, ['RR', 'rr'])),
    rrL: toNullableNumber(readFirst(dto, ['RRLowerBound', 'rrLowerBound'])),
    rrH: toNullableNumber(readFirst(dto, ['RRUpperBound', 'rrUpperBound'])),
    nnh: isNnt ? null : numberNeeded,
    nnhL: isNnt ? null : numberNeededLower,
    nnhH: isNnt ? null : numberNeededUpper,
    nnt: isNnt ? numberNeeded : null,
    nntL: isNnt ? numberNeededLower : null,
    nntH: isNnt ? numberNeededUpper : null,
    type: numberNeededKind,
    eT: toNullableNumber(readFirst(dto, ['EventsTreatment', 'eventsTreatment'])),
    eC: toNullableNumber(readFirst(dto, ['EventsComparator', 'eventsComparator'])),
    armN: toNullableNumber(readFirst(dto, ['ArmN', 'armN'])),
    comparatorN: toNullableNumber(readFirst(dto, ['ComparatorN', 'comparatorN'])),
    isPlac: toBoolean(readFirst(dto, ['IsPlaceboControlled', 'isPlaceboControlled'])),
    combo: toBoolean(readFirst(dto, ['IsCombo', 'isCombo'])),
    prec: normalizePrecisionClass(readFirst(dto, ['PrecisionClass', 'precisionClass'])),
    sig: isSignificant,
    prot: isProtective,
    riskSignificance,
    flags: normalizeFlags(readFirst(dto, ['Flags', 'flags', 'CalculationFlags', 'calculationFlags'])),
    tier: toDisplayString(readFirst(dto, ['CounselingTier', 'counselingTier']), ''),
    dose: toNullableNumber(readFirst(dto, ['Dose', 'dose'])),
    doseUnit: toDisplayString(readFirst(dto, ['DoseUnit', 'doseUnit'])),
    studyContext: toDisplayString(readFirst(dto, ['StudyContext', 'studyContext'])),
    population: toDisplayString(readFirst(dto, ['Population', 'population'])),
    subpopulation: toDisplayString(readFirst(dto, ['Subpopulation', 'subpopulation'])),
  };
}

/**************************************************************/
/**
 * Normalizes the triage payload enough for Phase 2 product hydration and term suggestions.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API triage DTO.
 * @returns {{ product: object | null, tiers: object[] }} Triage view model.
 */
export function normalizeTriage(payload) {
  // The Product property carries context for deep-linked products.
  const product = normalizeProduct(readFirst(payload, ['Product', 'product']));

  // Tier payloads are normalized defensively because empty tiers are valid.
  const tierPayloads = readFirst(payload, ['Tiers', 'tiers']);

  // Tiers default to an empty collection when absent.
  const tiers = Array.isArray(tierPayloads)
    ? tierPayloads.map((tier) => {
        // Signals stay normalized so suggestions can reuse live AE terms.
        const signalPayloads = readFirst(tier, ['Signals', 'signals']);

        return {
          tier: toDisplayString(readFirst(tier, ['Tier', 'tier'])),
          name: toDisplayString(readFirst(tier, ['Name', 'name']), 'Tier'),
          description: toDisplayString(readFirst(tier, ['Description', 'description'])),
          signals: Array.isArray(signalPayloads)
            ? signalPayloads.map((signal) => normalizeSignal(signal)).filter(Boolean)
            : [],
        };
      })
    : [];

  return { product, tiers };
}

/**************************************************************/
/**
 * Normalizes the forest payload into chart-ready signals and ticks.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API forest DTO.
 * @returns {{ signals: object[], axisTicks: number[] }} Forest view model.
 */
export function normalizeForest(payload) {
  // The forest endpoint returns a flat signal list.
  const signalPayloads = readFirst(payload, ['Signals', 'signals']);

  // Axis ticks default to the prototype's log-scale anchors.
  const tickPayloads = readFirst(payload, ['AxisTicks', 'axisTicks']);

  // Defensive array normalization keeps chart rendering stable on empty payloads.
  const signals = Array.isArray(signalPayloads)
    ? signalPayloads.map((signal) => normalizeSignal(signal)).filter(Boolean)
    : [];

  // Tick values must be finite positive numbers to be useful on a log axis.
  const axisTicks = Array.isArray(tickPayloads)
    ? tickPayloads
        .map((tick) => toNullableNumber(tick))
        .filter((tick) => tick !== null && tick > 0)
    : [0.1, 0.25, 0.5, 1, 2, 4, 10];

  return { signals, axisTicks };
}

/**************************************************************/
/**
 * Normalizes one quadrant point from the API.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API quadrant point DTO.
 * @returns {object | null} Quadrant point view model or null.
 */
function normalizeQuadrantPoint(dto) {
  // Missing points cannot render a meaningful chart dot.
  if (!dto) {
    return null;
  }

  // The nested signal carries the term, RR, significance, and flags.
  const signal = normalizeSignal(readFirst(dto, ['Signal', 'signal']));

  // Points without a signal are ignored.
  if (!signal) {
    return null;
  }

  // Direction can be precomputed or derived from the normalized signal.
  const direction = normalizeDirection(readFirst(dto, ['Direction', 'direction']) ?? signal.riskSignificance);

  return {
    id: toDisplayString(
      readFirst(dto, [
        'EncryptedFlattenedAdverseEventRiskTableID',
        'encryptedFlattenedAdverseEventRiskTableID',
      ]),
      signal.id,
    ),
    signal,
    x: toNullableNumber(readFirst(dto, ['PrecisionX', 'precisionX'])) ?? 0,
    y: toNullableNumber(readFirst(dto, ['MagnitudeY', 'magnitudeY'])) ?? 0,
    size: toNullableNumber(readFirst(dto, ['BubbleSize', 'bubbleSize'])) ?? 10,
    direction,
  };
}

/**************************************************************/
/**
 * Normalizes the quadrant payload into chart-ready points.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API quadrant DTO.
 * @returns {{ points: object[] }} Quadrant view model.
 */
export function normalizeQuadrant(payload) {
  // The endpoint returns point DTOs under Points.
  const pointPayloads = readFirst(payload, ['Points', 'points']);

  // Defensive array normalization keeps empty products easy to render.
  const points = Array.isArray(pointPayloads)
    ? pointPayloads.map((point) => normalizeQuadrantPoint(point)).filter(Boolean)
    : [];

  return { points };
}
