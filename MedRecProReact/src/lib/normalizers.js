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

// ASP.NET Core serializes enums as numbers unless a string converter is applied.
// The interchange UI groups rows by stable lowercase tokens, so tolerate both
// numeric wire values and future string enum values from the API.
const INTERCHANGE_CLASSIFICATION_TOKENS = {
  0: 'onlya',
  1: 'onlyb',
  2: 'similar',
  3: 'aworse',
  4: 'bworse',
  onlya: 'onlya',
  onlyb: 'onlyb',
  similar: 'similar',
  aworse: 'aworse',
  bworse: 'bworse',
};

const REVERSE_LOOKUP_VERDICT_TOKENS = {
  0: 'plausiblycausal',
  1: 'protective',
  2: 'notsignificantlyelevated',
  3: 'lowconfidence',
  plausiblycausal: 'plausiblycausal',
  protective: 'protective',
  notsignificantlyelevated: 'notsignificantlyelevated',
  lowconfidence: 'lowconfidence',
};

const CORRELATION_COMPARATOR_TOKENS = {
  0: 'Placebo',
  1: 'Active',
  2: 'Both',
  placebo: 'Placebo',
  active: 'Active',
  both: 'Both',
};

const CORRELATION_METHOD_TOKENS = {
  0: 'Spearman',
  1: 'Pearson',
  spearman: 'Spearman',
  pearson: 'Pearson',
};

const CORRELATION_AGGREGATION_TOKENS = {
  0: 'MedianLogRr',
  1: 'MeanLogRr',
  medianlogrr: 'MedianLogRr',
  meanlogrr: 'MeanLogRr',
};

/**************************************************************/
/**
 * Normalizes interchange enum values to UI grouping tokens.
 *
 * @param {unknown} value - API enum value, numeric or string.
 * @returns {'onlya' | 'onlyb' | 'similar' | 'aworse' | 'bworse'} Stable classification token.
 */
function normalizeInterchangeClassification(value) {
  // Separators are removed so "A_Worse", "A-Worse", and "A Worse" all group.
  const token = toToken(value, 'similar').replace(/[\s_-]/g, '');

  return INTERCHANGE_CLASSIFICATION_TOKENS[token] ?? 'similar';
}

/**************************************************************/
/**
 * Normalizes reverse-lookup verdict enum values to UI tokens.
 *
 * @param {unknown} value - API enum value, numeric or string.
 * @returns {'plausiblycausal' | 'protective' | 'notsignificantlyelevated' | 'lowconfidence'} Stable verdict token.
 */
function normalizeReverseLookupVerdict(value) {
  // Separators are removed so enum names and display labels share one path.
  const token = toToken(value, 'notsignificantlyelevated').replace(/[\s_-]/g, '');

  return REVERSE_LOOKUP_VERDICT_TOKENS[token] ?? 'notsignificantlyelevated';
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
 * Normalizes class-correlation comparator values to server enum names.
 *
 * @param {unknown} value - API enum value, numeric or string.
 * @returns {'Placebo' | 'Active' | 'Both'} Stable comparator value.
 */
function normalizeCorrelationComparator(value) {
  const token = toToken(value, 'placebo').replace(/[\s_-]/g, '');

  return CORRELATION_COMPARATOR_TOKENS[token] ?? 'Placebo';
}

/**************************************************************/
/**
 * Normalizes correlation-method values to server enum names.
 *
 * @param {unknown} value - API enum value, numeric or string.
 * @returns {'Spearman' | 'Pearson'} Stable method value.
 */
function normalizeCorrelationMethod(value) {
  const token = toToken(value, 'spearman').replace(/[\s_-]/g, '');

  return CORRELATION_METHOD_TOKENS[token] ?? 'Spearman';
}

/**************************************************************/
/**
 * Normalizes correlation aggregation values to server enum names.
 *
 * @param {unknown} value - API enum value, numeric or string.
 * @returns {'MedianLogRr' | 'MeanLogRr'} Stable aggregation value.
 */
function normalizeCorrelationAggregation(value) {
  const token = toToken(value, 'medianlogrr').replace(/[\s_-]/g, '');

  return CORRELATION_AGGREGATION_TOKENS[token] ?? 'MedianLogRr';
}

/**************************************************************/
/**
 * Normalizes the shared correlation filter echo.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API filter DTO.
 * @returns {object} Filter view model.
 */
function normalizeCorrelationFilters(dto) {
  const includeNonSignificant = readFirst(dto, ['IncludeNonSignificant', 'includeNonSignificant']);
  const excludeFragile = readFirst(dto, ['ExcludeFragile', 'excludeFragile']);

  return {
    comparator: normalizeCorrelationComparator(readFirst(dto, ['Comparator', 'comparator'])),
    includeNonSignificant: includeNonSignificant === undefined ? true : toBoolean(includeNonSignificant),
    excludeFragile: excludeFragile === undefined ? true : toBoolean(excludeFragile),
    minDrugsPerCell: toNullableNumber(readFirst(dto, ['MinDrugsPerCell', 'minDrugsPerCell'])) ?? 4,
    method: normalizeCorrelationMethod(readFirst(dto, ['Method', 'method'])),
    aggregation: normalizeCorrelationAggregation(readFirst(dto, ['Aggregation', 'aggregation'])),
    seriousSocOnly: toBoolean(readFirst(dto, ['SeriousSocOnly', 'seriousSocOnly'])),
    excludeCombos: toBoolean(readFirst(dto, ['ExcludeCombos', 'excludeCombos'])),
    minEvents: toNullableNumber(readFirst(dto, ['MinEvents', 'minEvents'])) ?? 0,
  };
}

/**************************************************************/
/**
 * Normalizes the MedDRA-system correlation filter echo.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API system filter DTO.
 * @returns {object} Filter view model.
 */
export function normalizeSystemCorrelationFilters(dto) {
  const includeNonSignificant = readFirst(dto, ['IncludeNonSignificant', 'includeNonSignificant']);
  const excludeFragile = readFirst(dto, ['ExcludeFragile', 'excludeFragile']);

  return {
    comparator: normalizeCorrelationComparator(readFirst(dto, ['Comparator', 'comparator'])),
    includeNonSignificant: includeNonSignificant === undefined ? true : toBoolean(includeNonSignificant),
    excludeFragile: excludeFragile === undefined ? true : toBoolean(excludeFragile),
    minTermsPerCell: toNullableNumber(readFirst(dto, ['MinTermsPerCell', 'minTermsPerCell'])) ?? 4,
    method: normalizeCorrelationMethod(readFirst(dto, ['Method', 'method'])),
    aggregation: normalizeCorrelationAggregation(readFirst(dto, ['Aggregation', 'aggregation'])),
    excludeCombos: toBoolean(readFirst(dto, ['ExcludeCombos', 'excludeCombos'])),
    minEvents: toNullableNumber(readFirst(dto, ['MinEvents', 'minEvents'])) ?? 0,
  };
}

/**************************************************************/
/**
 * Normalizes warning text arrays without dropping backend guidance.
 *
 * @param {unknown} warnings - API warning payload.
 * @returns {string[]} Warning strings.
 */
function normalizeWarnings(warnings) {
  if (!Array.isArray(warnings)) {
    return [];
  }

  return warnings.map((warning) => toDisplayString(warning)).filter(Boolean);
}

/**************************************************************/
/**
 * Normalizes a pharmacologic class picker row.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API picker DTO.
 * @returns {object | null} Class picker view model or null.
 */
export function normalizeCorrelationClass(dto) {
  const pharmClassCode = toDisplayString(readFirst(dto, ['PharmClassCode', 'pharmClassCode']));

  if (!pharmClassCode) {
    return null;
  }

  const explicitRenderableMap = readFirst(dto, ['HasRenderableMap', 'hasRenderableMap']);
  const hasRenderableMap = explicitRenderableMap !== undefined
    ? toBoolean(explicitRenderableMap)
    : toBoolean(readFirst(dto, ['IsCorrelatable', 'isCorrelatable']));

  return {
    id: pharmClassCode,
    pharmClassCode,
    pharmClassName: toDisplayString(readFirst(dto, ['PharmClassName', 'pharmClassName']), pharmClassCode),
    encryptedPharmacologicClassId: toDisplayString(
      readFirst(dto, [
        'EncryptedPharmacologicClassID',
        'EncryptedPharmacologicClassId',
        'encryptedPharmacologicClassID',
        'encryptedPharmacologicClassId',
      ]),
    ),
    drugCount: toNullableNumber(readFirst(dto, ['DrugCount', 'drugCount'])) ?? 0,
    socCount: toNullableNumber(readFirst(dto, ['SocCount', 'socCount'])) ?? 0,
    totalOffDiagonalCellCount: toNullableNumber(
      readFirst(dto, ['TotalOffDiagonalCellCount', 'totalOffDiagonalCellCount']),
    ) ?? 0,
    usableMapCellCount: toNullableNumber(readFirst(dto, ['UsableMapCellCount', 'usableMapCellCount'])) ?? 0,
    maxPairCount: toNullableNumber(readFirst(dto, ['MaxPairCount', 'maxPairCount'])) ?? 0,
    hasRenderableMap,
    renderabilityReason: toDisplayString(readFirst(dto, ['RenderabilityReason', 'renderabilityReason'])),
    isCorrelatable: hasRenderableMap,
  };
}

/**************************************************************/
/**
 * Compares class picker rows by renderability and population.
 *
 * @param {object} left - Left class row.
 * @param {object} right - Right class row.
 * @returns {number} Sort comparison result.
 */
function compareCorrelationClasses(left, right) {
  const leftName = (left.pharmClassName || left.pharmClassCode || '').toLowerCase();
  const rightName = (right.pharmClassName || right.pharmClassCode || '').toLowerCase();

  return Number(right.hasRenderableMap) - Number(left.hasRenderableMap)
    || right.usableMapCellCount - left.usableMapCellCount
    || right.maxPairCount - left.maxPairCount
    || right.drugCount - left.drugCount
    || right.socCount - left.socCount
    || leftName.localeCompare(rightName)
    || left.pharmClassCode.localeCompare(right.pharmClassCode);
}

/**************************************************************/
/**
 * Normalizes class picker payloads and keeps map-ready rows first.
 *
 * @param {unknown} payload - API picker payload.
 * @returns {object[]} Class picker rows.
 */
export function normalizeCorrelationClasses(payload) {
  const items = Array.isArray(payload)
    ? payload
    : Array.isArray(payload?.items)
      ? payload.items
      : [];

  return items
    .map((item) => normalizeCorrelationClass(item))
    .filter(Boolean)
    .sort(compareCorrelationClasses);
}

/**************************************************************/
/**
 * Normalizes a class-picker page with items and pagination metadata.
 *
 * @param {unknown} payload - API class-picker page payload.
 * @returns {{ items: object[], totalCount: number, chartableCount: number, pageNumber: number | null, pageSize: number | null }} Normalized page.
 */
export function normalizeCorrelationClassPage(payload) {
  const items = normalizeCorrelationClasses(payload);
  const totalCount = toNullableNumber(readFirst(payload, ['TotalCount', 'totalCount'])) ?? items.length;
  const chartableCount = toNullableNumber(readFirst(payload, ['ChartableCount', 'chartableCount']))
    ?? items.filter((item) => item.hasRenderableMap).length;

  return {
    items,
    totalCount,
    chartableCount,
    pageNumber: toNullableNumber(readFirst(payload, ['PageNumber', 'pageNumber'])),
    pageSize: toNullableNumber(readFirst(payload, ['PageSize', 'pageSize'])),
  };
}

/**************************************************************/
/**
 * Normalizes page metadata embedded in correlation response bodies.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API page DTO.
 * @param {{ pageNumber?: number, pageSize?: number }} fallback - Fallback values.
 * @returns {{ pageNumber: number, pageSize: number, totalCount: number, totalPages: number, hasPreviousPage: boolean, hasNextPage: boolean }} Page view model.
 */
export function normalizeAxisPage(dto, fallback = {}) {
  const pageNumber = toNullableNumber(readFirst(dto, ['PageNumber', 'pageNumber'])) ?? fallback.pageNumber ?? 1;
  const pageSize = toNullableNumber(readFirst(dto, ['PageSize', 'pageSize'])) ?? fallback.pageSize ?? 0;
  const totalCount = toNullableNumber(readFirst(dto, ['TotalCount', 'totalCount'])) ?? 0;
  const totalPages = toNullableNumber(readFirst(dto, ['TotalPages', 'totalPages']))
    ?? (pageSize > 0 ? Math.max(1, Math.ceil(totalCount / pageSize)) : 1);

  return {
    pageNumber,
    pageSize,
    totalCount,
    totalPages,
    hasPreviousPage: toBoolean(readFirst(dto, ['HasPreviousPage', 'hasPreviousPage'])),
    hasNextPage: toBoolean(readFirst(dto, ['HasNextPage', 'hasNextPage'])),
  };
}

/**************************************************************/
/**
 * Normalizes one MedDRA system picker row.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API picker DTO.
 * @returns {object | null} System picker view model or null.
 */
export function normalizeCorrelationSystem(dto) {
  const systemOrganClass = toDisplayString(readFirst(dto, ['SystemOrganClass', 'systemOrganClass']));

  if (!systemOrganClass) {
    return null;
  }

  return {
    id: systemOrganClass,
    systemOrganClass,
    classCount: toNullableNumber(readFirst(dto, ['ClassCount', 'classCount'])) ?? 0,
    drugCount: toNullableNumber(readFirst(dto, ['DrugCount', 'drugCount'])) ?? 0,
    termCount: toNullableNumber(readFirst(dto, ['TermCount', 'termCount'])) ?? 0,
    totalOffDiagonalCellCount: toNullableNumber(
      readFirst(dto, ['TotalOffDiagonalCellCount', 'totalOffDiagonalCellCount']),
    ) ?? 0,
    usableMapCellCount: toNullableNumber(readFirst(dto, ['UsableMapCellCount', 'usableMapCellCount'])) ?? 0,
    maxPairCount: toNullableNumber(readFirst(dto, ['MaxPairCount', 'maxPairCount'])) ?? 0,
    hasRenderableMap: toBoolean(readFirst(dto, ['HasRenderableMap', 'hasRenderableMap'])),
    renderabilityReason: toDisplayString(readFirst(dto, ['RenderabilityReason', 'renderabilityReason'])),
  };
}

/**************************************************************/
/**
 * Compares system picker rows by renderability and population.
 *
 * @param {object} left - Left system row.
 * @param {object} right - Right system row.
 * @returns {number} Sort comparison result.
 */
function compareCorrelationSystems(left, right) {
  return Number(right.hasRenderableMap) - Number(left.hasRenderableMap)
    || right.usableMapCellCount - left.usableMapCellCount
    || right.maxPairCount - left.maxPairCount
    || right.classCount - left.classCount
    || right.drugCount - left.drugCount
    || right.termCount - left.termCount
    || left.systemOrganClass.localeCompare(right.systemOrganClass);
}

/**************************************************************/
/**
 * Normalizes MedDRA system picker payloads and keeps map-ready rows first.
 *
 * @param {unknown} payload - API picker payload.
 * @returns {object[]} System picker rows.
 */
export function normalizeCorrelationSystems(payload) {
  const items = Array.isArray(payload)
    ? payload
    : Array.isArray(payload?.items)
      ? payload.items
      : [];

  return items
    .map((item) => normalizeCorrelationSystem(item))
    .filter(Boolean)
    .sort(compareCorrelationSystems);
}

/**************************************************************/
/**
 * Normalizes a system-picker page with items and pagination metadata.
 *
 * @param {unknown} payload - API system-picker page payload.
 * @returns {{ items: object[], totalCount: number, chartableCount: number, pageNumber: number | null, pageSize: number | null }} Normalized page.
 */
export function normalizeCorrelationSystemPage(payload) {
  const items = normalizeCorrelationSystems(payload);
  const totalCount = toNullableNumber(readFirst(payload, ['TotalCount', 'totalCount'])) ?? items.length;
  const chartableCount = toNullableNumber(readFirst(payload, ['ChartableCount', 'chartableCount']))
    ?? items.filter((item) => item.hasRenderableMap).length;

  return {
    items,
    totalCount,
    chartableCount,
    pageNumber: toNullableNumber(readFirst(payload, ['PageNumber', 'pageNumber'])),
    pageSize: toNullableNumber(readFirst(payload, ['PageSize', 'pageSize'])),
  };
}

/**************************************************************/
/**
 * Normalizes one class axis item in a system-scoped response.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API axis DTO.
 * @returns {object | null} Axis item view model or null.
 */
export function normalizeSystemClassAxisItem(dto) {
  if (!dto) {
    return null;
  }

  const index = toNullableNumber(readFirst(dto, ['Index', 'index']));
  const pharmClassCode = toDisplayString(readFirst(dto, ['PharmClassCode', 'pharmClassCode']));

  if (index === null || !pharmClassCode) {
    return null;
  }

  return {
    index,
    id: pharmClassCode,
    pharmClassCode,
    pharmClassName: toDisplayString(readFirst(dto, ['PharmClassName', 'pharmClassName']), pharmClassCode),
    encryptedPharmacologicClassId: toDisplayString(
      readFirst(dto, [
        'EncryptedPharmacologicClassID',
        'EncryptedPharmacologicClassId',
        'encryptedPharmacologicClassID',
        'encryptedPharmacologicClassId',
      ]),
    ),
    termCount: toNullableNumber(readFirst(dto, ['TermCount', 'termCount'])) ?? 0,
    drugCount: toNullableNumber(readFirst(dto, ['DrugCount', 'drugCount'])) ?? 0,
    hasRenderableMap: toBoolean(readFirst(dto, ['HasRenderableMap', 'hasRenderableMap'])),
  };
}

/**************************************************************/
/**
 * Normalizes one class-pair cell in a system-scoped map.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API cell DTO.
 * @returns {object | null} Cell view model or null.
 */
function normalizeSystemCorrelationMapCell(dto) {
  if (!dto) {
    return null;
  }

  const rowIndex = toNullableNumber(readFirst(dto, ['RowIndex', 'rowIndex']));
  const columnIndex = toNullableNumber(readFirst(dto, ['ColumnIndex', 'columnIndex']));

  if (rowIndex === null || columnIndex === null) {
    return null;
  }

  return {
    id: `${rowIndex}:${columnIndex}`,
    rowIndex,
    columnIndex,
    rowClassCode: toDisplayString(readFirst(dto, ['RowClassCode', 'rowClassCode'])),
    columnClassCode: toDisplayString(readFirst(dto, ['ColumnClassCode', 'columnClassCode'])),
    coefficient: toNullableNumber(readFirst(dto, ['Coefficient', 'coefficient'])),
    pairCount: toNullableNumber(readFirst(dto, ['PairCount', 'pairCount'])) ?? 0,
    pValue: toNullableNumber(readFirst(dto, ['PValue', 'pValue'])),
    isSignificant: toBoolean(readFirst(dto, ['IsSignificant', 'isSignificant'])),
    isFragile: toBoolean(readFirst(dto, ['IsFragile', 'isFragile'])),
    insufficientN: toBoolean(readFirst(dto, ['InsufficientN', 'insufficientN'])),
    isDiagonal: toBoolean(readFirst(dto, ['IsDiagonal', 'isDiagonal'])),
  };
}

/**************************************************************/
/**
 * Normalizes one system-scoped class marginal summary.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API summary DTO.
 * @returns {object | null} Summary view model or null.
 */
function normalizeSystemClassSummary(dto) {
  if (!dto) {
    return null;
  }

  const index = toNullableNumber(readFirst(dto, ['Index', 'index']));
  const pharmClassCode = toDisplayString(readFirst(dto, ['PharmClassCode', 'pharmClassCode']));

  if (index === null || !pharmClassCode) {
    return null;
  }

  return {
    index,
    pharmClassCode,
    pharmClassName: toDisplayString(readFirst(dto, ['PharmClassName', 'pharmClassName']), pharmClassCode),
    drugCount: toNullableNumber(readFirst(dto, ['DrugCount', 'drugCount'])) ?? 0,
    termCount: toNullableNumber(readFirst(dto, ['TermCount', 'termCount'])) ?? 0,
    medianLogRr: toNullableNumber(readFirst(dto, ['MedianLogRr', 'medianLogRr'])),
    medianRr: toNullableNumber(readFirst(dto, ['MedianRr', 'medianRr'])),
    elevatedShare: toNullableNumber(readFirst(dto, ['ElevatedShare', 'elevatedShare'])) ?? 0,
    protectiveShare: toNullableNumber(readFirst(dto, ['ProtectiveShare', 'protectiveShare'])) ?? 0,
  };
}

/**************************************************************/
/**
 * Normalizes a system-scoped class by class correlation map.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API map DTO.
 * @returns {object | null} Map view model or null.
 */
export function normalizeSystemCorrelationMap(payload) {
  if (!payload) {
    return null;
  }

  const selectedSystems = readFirst(payload, ['SelectedSystems', 'selectedSystems']);
  const classPayloads = readFirst(payload, ['Classes', 'classes']);
  const cellPayloads = readFirst(payload, ['Cells', 'cells']);
  const summaryPayloads = readFirst(payload, ['ClassSummaries', 'classSummaries']);

  return {
    selectedSystems: Array.isArray(selectedSystems)
      ? selectedSystems.map((system) => toDisplayString(system)).filter(Boolean)
      : [],
    appliedFilters: normalizeSystemCorrelationFilters(readFirst(payload, ['AppliedFilters', 'appliedFilters'])),
    classCount: toNullableNumber(readFirst(payload, ['ClassCount', 'classCount'])) ?? 0,
    includesFullMatrix: toBoolean(readFirst(payload, ['IncludesFullMatrix', 'includesFullMatrix'])),
    classPage: normalizeAxisPage(readFirst(payload, ['ClassPage', 'classPage']), { pageNumber: 1, pageSize: 40 }),
    classes: Array.isArray(classPayloads)
      ? classPayloads.map((item) => normalizeSystemClassAxisItem(item)).filter(Boolean)
      : [],
    cells: Array.isArray(cellPayloads)
      ? cellPayloads.map((cell) => normalizeSystemCorrelationMapCell(cell)).filter(Boolean)
      : [],
    classSummaries: Array.isArray(summaryPayloads)
      ? summaryPayloads.map((summary) => normalizeSystemClassSummary(summary)).filter(Boolean)
      : [],
    warnings: normalizeWarnings(readFirst(payload, ['Warnings', 'warnings'])),
  };
}

/**************************************************************/
/**
 * Normalizes one drug column in a system-scoped heatmap.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API drug DTO.
 * @returns {object | null} Drug view model or null.
 */
function normalizeSystemHeatmapDrug(dto) {
  if (!dto) {
    return null;
  }

  const index = toNullableNumber(readFirst(dto, ['Index', 'index']));
  const drugDisplayName = toDisplayString(readFirst(dto, ['DrugDisplayName', 'drugDisplayName']));

  if (index === null || !drugDisplayName) {
    return null;
  }

  return {
    index,
    id: `${index}:${drugDisplayName}`,
    encryptedActiveMoietyId: toDisplayString(
      readFirst(dto, [
        'EncryptedActiveMoietyID',
        'EncryptedActiveMoietyId',
        'encryptedActiveMoietyID',
        'encryptedActiveMoietyId',
      ]),
    ),
    drugDisplayName,
    documentGuid: toDisplayString(readFirst(dto, ['DocumentGUID', 'DocumentGuid', 'documentGUID', 'documentGuid'])),
  };
}

/**************************************************************/
/**
 * Normalizes one populated class by drug heatmap cell.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API heatmap cell DTO.
 * @returns {object | null} Heatmap cell view model or null.
 */
function normalizeSystemHeatmapCell(dto) {
  if (!dto) {
    return null;
  }

  const classIndex = toNullableNumber(readFirst(dto, ['ClassIndex', 'classIndex']));
  const drugIndex = toNullableNumber(readFirst(dto, ['DrugIndex', 'drugIndex']));

  if (classIndex === null || drugIndex === null) {
    return null;
  }

  return {
    id: `${classIndex}:${drugIndex}`,
    classIndex,
    drugIndex,
    logRr: toNullableNumber(readFirst(dto, ['LogRr', 'logRr'])),
    rr: toNullableNumber(readFirst(dto, ['Rr', 'RR', 'rr'])),
    precision: normalizePrecisionClass(readFirst(dto, ['Precision', 'precision'])),
    significance: normalizeDirection(readFirst(dto, ['Significance', 'significance'])),
    termCount: toNullableNumber(readFirst(dto, ['TermCount', 'termCount'])) ?? 0,
  };
}

/**************************************************************/
/**
 * Normalizes a system-scoped class by drug heatmap.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API heatmap DTO.
 * @returns {object | null} Heatmap view model or null.
 */
export function normalizeSystemCorrelationHeatmap(payload) {
  if (!payload) {
    return null;
  }

  const selectedSystems = readFirst(payload, ['SelectedSystems', 'selectedSystems']);
  const classPayloads = readFirst(payload, ['Classes', 'classes']);
  const drugPayloads = readFirst(payload, ['Drugs', 'drugs']);
  const cellPayloads = readFirst(payload, ['Cells', 'cells']);

  return {
    selectedSystems: Array.isArray(selectedSystems)
      ? selectedSystems.map((system) => toDisplayString(system)).filter(Boolean)
      : [],
    appliedFilters: normalizeSystemCorrelationFilters(readFirst(payload, ['AppliedFilters', 'appliedFilters'])),
    classPage: normalizeAxisPage(readFirst(payload, ['ClassPage', 'classPage']), { pageNumber: 1, pageSize: 40 }),
    drugPage: normalizeAxisPage(readFirst(payload, ['DrugPage', 'drugPage']), { pageNumber: 1, pageSize: 50 }),
    classes: Array.isArray(classPayloads)
      ? classPayloads.map((item) => normalizeSystemClassAxisItem(item)).filter(Boolean)
      : [],
    drugs: Array.isArray(drugPayloads)
      ? drugPayloads.map((drug) => normalizeSystemHeatmapDrug(drug)).filter(Boolean)
      : [],
    cells: Array.isArray(cellPayloads)
      ? cellPayloads.map((cell) => normalizeSystemHeatmapCell(cell)).filter(Boolean)
      : [],
    warnings: normalizeWarnings(readFirst(payload, ['Warnings', 'warnings'])),
  };
}

/**************************************************************/
/**
 * Normalizes one selected-system term pair behind a class-pair cell.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API term-pair DTO.
 * @returns {object | null} Term-pair view model or null.
 */
function normalizeSystemTermPair(dto) {
  if (!dto) {
    return null;
  }

  const parameterName = toDisplayString(readFirst(dto, ['ParameterName', 'parameterName']));

  if (!parameterName) {
    return null;
  }

  return {
    id: `${toDisplayString(readFirst(dto, ['SystemOrganClass', 'systemOrganClass']))}:${parameterName}`,
    systemOrganClass: toDisplayString(readFirst(dto, ['SystemOrganClass', 'systemOrganClass'])),
    parameterName,
    logRrX: toNullableNumber(readFirst(dto, ['LogRrX', 'logRrX'])),
    logRrY: toNullableNumber(readFirst(dto, ['LogRrY', 'logRrY'])),
    rrX: toNullableNumber(readFirst(dto, ['RrX', 'RRX', 'rrX'])),
    rrY: toNullableNumber(readFirst(dto, ['RrY', 'RRY', 'rrY'])),
    precisionX: normalizePrecisionClass(readFirst(dto, ['PrecisionX', 'precisionX'])),
    precisionY: normalizePrecisionClass(readFirst(dto, ['PrecisionY', 'precisionY'])),
    significanceX: normalizeDirection(readFirst(dto, ['SignificanceX', 'significanceX'])),
    significanceY: normalizeDirection(readFirst(dto, ['SignificanceY', 'significanceY'])),
    drugCountX: toNullableNumber(readFirst(dto, ['DrugCountX', 'drugCountX'])) ?? 0,
    drugCountY: toNullableNumber(readFirst(dto, ['DrugCountY', 'drugCountY'])) ?? 0,
    termCountX: toNullableNumber(readFirst(dto, ['TermCountX', 'termCountX'])) ?? 0,
    termCountY: toNullableNumber(readFirst(dto, ['TermCountY', 'termCountY'])) ?? 0,
  };
}

/**************************************************************/
/**
 * Normalizes selected-system term-pair detail behind one class-pair cell.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API cell-detail DTO.
 * @returns {object | null} Cell-detail view model or null.
 */
export function normalizeSystemCorrelationCellDetail(payload) {
  if (!payload) {
    return null;
  }

  const selectedSystems = readFirst(payload, ['SelectedSystems', 'selectedSystems']);
  const pairPayloads = readFirst(payload, ['TermPairs', 'termPairs']);

  return {
    selectedSystems: Array.isArray(selectedSystems)
      ? selectedSystems.map((system) => toDisplayString(system)).filter(Boolean)
      : [],
    classX: normalizeSystemClassAxisItem(readFirst(payload, ['ClassX', 'classX'])),
    classY: normalizeSystemClassAxisItem(readFirst(payload, ['ClassY', 'classY'])),
    appliedFilters: normalizeSystemCorrelationFilters(readFirst(payload, ['AppliedFilters', 'appliedFilters'])),
    coefficient: toNullableNumber(readFirst(payload, ['Coefficient', 'coefficient'])),
    rawCoefficient: toNullableNumber(readFirst(payload, ['RawCoefficient', 'rawCoefficient'])),
    pValue: toNullableNumber(readFirst(payload, ['PValue', 'pValue'])),
    rawPValue: toNullableNumber(readFirst(payload, ['RawPValue', 'rawPValue'])),
    isSignificant: toBoolean(readFirst(payload, ['IsSignificant', 'isSignificant'])),
    pairCount: toNullableNumber(readFirst(payload, ['PairCount', 'pairCount'])) ?? 0,
    minTermsPerCell: toNullableNumber(readFirst(payload, ['MinTermsPerCell', 'minTermsPerCell'])) ?? 4,
    insufficientN: toBoolean(readFirst(payload, ['InsufficientN', 'insufficientN'])),
    isDiagonal: toBoolean(readFirst(payload, ['IsDiagonal', 'isDiagonal'])),
    termPairPage: normalizeAxisPage(readFirst(payload, ['TermPairPage', 'termPairPage']), {
      pageNumber: 1,
      pageSize: 100,
    }),
    termPairs: Array.isArray(pairPayloads)
      ? pairPayloads.map((pair) => normalizeSystemTermPair(pair)).filter(Boolean)
      : [],
    warnings: normalizeWarnings(readFirst(payload, ['Warnings', 'warnings'])),
  };
}

/**************************************************************/
/**
 * Normalizes one SOC by SOC correlation map cell.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API cell DTO.
 * @returns {object | null} Cell view model or null.
 */
function normalizeCorrelationMapCell(dto) {
  if (!dto) {
    return null;
  }

  const rowIndex = toNullableNumber(readFirst(dto, ['RowIndex', 'rowIndex']));
  const columnIndex = toNullableNumber(readFirst(dto, ['ColumnIndex', 'columnIndex']));

  if (rowIndex === null || columnIndex === null) {
    return null;
  }

  return {
    id: `${rowIndex}:${columnIndex}`,
    rowIndex,
    columnIndex,
    rowSoc: toDisplayString(readFirst(dto, ['RowSoc', 'rowSoc'])),
    columnSoc: toDisplayString(readFirst(dto, ['ColumnSoc', 'columnSoc'])),
    coefficient: toNullableNumber(readFirst(dto, ['Coefficient', 'coefficient'])),
    pairCount: toNullableNumber(readFirst(dto, ['PairCount', 'pairCount'])) ?? 0,
    pValue: toNullableNumber(readFirst(dto, ['PValue', 'pValue'])),
    isSignificant: toBoolean(readFirst(dto, ['IsSignificant', 'isSignificant'])),
    isFragile: toBoolean(readFirst(dto, ['IsFragile', 'isFragile'])),
    insufficientN: toBoolean(readFirst(dto, ['InsufficientN', 'insufficientN'])),
    isDiagonal: toBoolean(readFirst(dto, ['IsDiagonal', 'isDiagonal'])),
  };
}

/**************************************************************/
/**
 * Normalizes one per-SOC correlation summary.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API summary DTO.
 * @returns {object | null} Summary view model or null.
 */
function normalizeCorrelationSocSummary(dto) {
  if (!dto) {
    return null;
  }

  const index = toNullableNumber(readFirst(dto, ['Index', 'index']));
  const soc = toDisplayString(readFirst(dto, ['Soc', 'soc']));

  if (index === null || !soc) {
    return null;
  }

  return {
    index,
    soc,
    drugCount: toNullableNumber(readFirst(dto, ['DrugCount', 'drugCount'])) ?? 0,
    fragileDrugCount: toNullableNumber(readFirst(dto, ['FragileDrugCount', 'fragileDrugCount'])) ?? 0,
    medianLogRr: toNullableNumber(readFirst(dto, ['MedianLogRr', 'medianLogRr'])),
    medianRr: toNullableNumber(readFirst(dto, ['MedianRr', 'medianRr'])),
    elevatedShare: toNullableNumber(readFirst(dto, ['ElevatedShare', 'elevatedShare'])) ?? 0,
    protectiveShare: toNullableNumber(readFirst(dto, ['ProtectiveShare', 'protectiveShare'])) ?? 0,
  };
}

/**************************************************************/
/**
 * Normalizes a SOC by SOC class-correlation map.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API map DTO.
 * @returns {object | null} Map view model or null.
 */
export function normalizeCorrelationMap(payload) {
  if (!payload) {
    return null;
  }

  const socPayload = readFirst(payload, ['Soc', 'soc']);
  const cellPayloads = readFirst(payload, ['Cells', 'cells']);
  const summaryPayloads = readFirst(payload, ['SocSummaries', 'socSummaries']);

  return {
    pharmClassCode: toDisplayString(readFirst(payload, ['PharmClassCode', 'pharmClassCode'])),
    pharmClassName: toDisplayString(readFirst(payload, ['PharmClassName', 'pharmClassName'])),
    encryptedPharmacologicClassId: toDisplayString(
      readFirst(payload, [
        'EncryptedPharmacologicClassID',
        'EncryptedPharmacologicClassId',
        'encryptedPharmacologicClassID',
        'encryptedPharmacologicClassId',
      ]),
    ),
    appliedFilters: normalizeCorrelationFilters(readFirst(payload, ['AppliedFilters', 'appliedFilters'])),
    drugCount: toNullableNumber(readFirst(payload, ['DrugCount', 'drugCount'])) ?? 0,
    soc: Array.isArray(socPayload) ? socPayload.map((soc) => toDisplayString(soc)).filter(Boolean) : [],
    cells: Array.isArray(cellPayloads)
      ? cellPayloads.map((cell) => normalizeCorrelationMapCell(cell)).filter(Boolean)
      : [],
    socSummaries: Array.isArray(summaryPayloads)
      ? summaryPayloads.map((summary) => normalizeCorrelationSocSummary(summary)).filter(Boolean)
      : [],
    warnings: normalizeWarnings(readFirst(payload, ['Warnings', 'warnings'])),
  };
}

/**************************************************************/
/**
 * Normalizes one heatmap drug column.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API drug DTO.
 * @returns {object | null} Drug view model or null.
 */
function normalizeCorrelationHeatmapDrug(dto) {
  if (!dto) {
    return null;
  }

  const drugDisplayName = toDisplayString(readFirst(dto, ['DrugDisplayName', 'drugDisplayName']));

  if (!drugDisplayName) {
    return null;
  }

  return {
    encryptedActiveMoietyId: toDisplayString(
      readFirst(dto, [
        'EncryptedActiveMoietyID',
        'EncryptedActiveMoietyId',
        'encryptedActiveMoietyID',
        'encryptedActiveMoietyId',
      ]),
    ),
    drugDisplayName,
    documentGuid: toDisplayString(readFirst(dto, ['DocumentGUID', 'DocumentGuid', 'documentGUID', 'documentGuid'])),
  };
}

/**************************************************************/
/**
 * Normalizes one populated SOC by drug heatmap cell.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API heatmap cell DTO.
 * @returns {object | null} Heatmap cell view model or null.
 */
function normalizeCorrelationHeatmapCell(dto) {
  if (!dto) {
    return null;
  }

  const socIndex = toNullableNumber(readFirst(dto, ['SocIndex', 'socIndex']));
  const drugIndex = toNullableNumber(readFirst(dto, ['DrugIndex', 'drugIndex']));

  if (socIndex === null || drugIndex === null) {
    return null;
  }

  return {
    id: `${socIndex}:${drugIndex}`,
    socIndex,
    drugIndex,
    logRr: toNullableNumber(readFirst(dto, ['LogRr', 'logRr'])),
    rr: toNullableNumber(readFirst(dto, ['Rr', 'RR', 'rr'])),
    precision: normalizePrecisionClass(readFirst(dto, ['Precision', 'precision'])),
    significance: normalizeDirection(readFirst(dto, ['Significance', 'significance'])),
    termCount: toNullableNumber(readFirst(dto, ['TermCount', 'termCount'])) ?? 0,
  };
}

/**************************************************************/
/**
 * Normalizes a sparse SOC by drug heatmap.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API heatmap DTO.
 * @returns {object | null} Heatmap view model or null.
 */
export function normalizeCorrelationHeatmap(payload) {
  if (!payload) {
    return null;
  }

  const socPayload = readFirst(payload, ['Soc', 'soc']);
  const drugPayloads = readFirst(payload, ['Drugs', 'drugs']);
  const cellPayloads = readFirst(payload, ['Cells', 'cells']);

  return {
    pharmClassCode: toDisplayString(readFirst(payload, ['PharmClassCode', 'pharmClassCode'])),
    pharmClassName: toDisplayString(readFirst(payload, ['PharmClassName', 'pharmClassName'])),
    encryptedPharmacologicClassId: toDisplayString(
      readFirst(payload, [
        'EncryptedPharmacologicClassID',
        'EncryptedPharmacologicClassId',
        'encryptedPharmacologicClassID',
        'encryptedPharmacologicClassId',
      ]),
    ),
    appliedFilters: normalizeCorrelationFilters(readFirst(payload, ['AppliedFilters', 'appliedFilters'])),
    drugCount: toNullableNumber(readFirst(payload, ['DrugCount', 'drugCount'])) ?? 0,
    soc: Array.isArray(socPayload) ? socPayload.map((soc) => toDisplayString(soc)).filter(Boolean) : [],
    drugs: Array.isArray(drugPayloads)
      ? drugPayloads.map((drug) => normalizeCorrelationHeatmapDrug(drug)).filter(Boolean)
      : [],
    cells: Array.isArray(cellPayloads)
      ? cellPayloads.map((cell) => normalizeCorrelationHeatmapCell(cell)).filter(Boolean)
      : [],
    warnings: normalizeWarnings(readFirst(payload, ['Warnings', 'warnings'])),
  };
}

/**************************************************************/
/**
 * Normalizes one per-drug pair behind a correlation cell.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API pair DTO.
 * @returns {object | null} Pair view model or null.
 */
function normalizeCorrelationDrugPair(dto) {
  if (!dto) {
    return null;
  }

  const drugDisplayName = toDisplayString(readFirst(dto, ['DrugDisplayName', 'drugDisplayName']));

  if (!drugDisplayName) {
    return null;
  }

  return {
    drugDisplayName,
    encryptedActiveMoietyId: toDisplayString(
      readFirst(dto, [
        'EncryptedActiveMoietyID',
        'EncryptedActiveMoietyId',
        'encryptedActiveMoietyID',
        'encryptedActiveMoietyId',
      ]),
    ),
    logRrX: toNullableNumber(readFirst(dto, ['LogRrX', 'logRrX'])),
    logRrY: toNullableNumber(readFirst(dto, ['LogRrY', 'logRrY'])),
    rrX: toNullableNumber(readFirst(dto, ['RrX', 'RRX', 'rrX'])),
    rrY: toNullableNumber(readFirst(dto, ['RrY', 'RRY', 'rrY'])),
    precisionX: normalizePrecisionClass(readFirst(dto, ['PrecisionX', 'precisionX'])),
    precisionY: normalizePrecisionClass(readFirst(dto, ['PrecisionY', 'precisionY'])),
    termCountX: toNullableNumber(readFirst(dto, ['TermCountX', 'termCountX'])) ?? 0,
    termCountY: toNullableNumber(readFirst(dto, ['TermCountY', 'termCountY'])) ?? 0,
  };
}

/**************************************************************/
/**
 * Normalizes the per-drug drill-down behind one correlation map cell.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API cell-detail DTO.
 * @returns {object | null} Cell-detail view model or null.
 */
export function normalizeCorrelationCellDetail(payload) {
  if (!payload) {
    return null;
  }

  const pairPayloads = readFirst(payload, ['DrugPairs', 'drugPairs']);

  return {
    pharmClassCode: toDisplayString(readFirst(payload, ['PharmClassCode', 'pharmClassCode'])),
    pharmClassName: toDisplayString(readFirst(payload, ['PharmClassName', 'pharmClassName'])),
    socX: toDisplayString(readFirst(payload, ['SocX', 'socX'])),
    socY: toDisplayString(readFirst(payload, ['SocY', 'socY'])),
    appliedFilters: normalizeCorrelationFilters(readFirst(payload, ['AppliedFilters', 'appliedFilters'])),
    coefficient: toNullableNumber(readFirst(payload, ['Coefficient', 'coefficient'])),
    rawCoefficient: toNullableNumber(readFirst(payload, ['RawCoefficient', 'rawCoefficient'])),
    pValue: toNullableNumber(readFirst(payload, ['PValue', 'pValue'])),
    rawPValue: toNullableNumber(readFirst(payload, ['RawPValue', 'rawPValue'])),
    isSignificant: toBoolean(readFirst(payload, ['IsSignificant', 'isSignificant'])),
    insufficientN: toBoolean(readFirst(payload, ['InsufficientN', 'insufficientN'])),
    isDiagonal: toBoolean(readFirst(payload, ['IsDiagonal', 'isDiagonal'])),
    minDrugsPerCell: toNullableNumber(readFirst(payload, ['MinDrugsPerCell', 'minDrugsPerCell'])) ?? 4,
    pairCount: toNullableNumber(readFirst(payload, ['PairCount', 'pairCount'])) ?? 0,
    drugPairs: Array.isArray(pairPayloads)
      ? pairPayloads.map((pair) => normalizeCorrelationDrugPair(pair)).filter(Boolean)
      : [],
    warnings: normalizeWarnings(readFirst(payload, ['Warnings', 'warnings'])),
  };
}

/**************************************************************/
/**
 * Normalizes the active-ingredient list into paired substance + EPC class entries.
 *
 * @param {unknown} payload - API ActiveIngredients array.
 * @returns {{ substance: string, pharmClass: string, unii: string }[]} Ingredient view models.
 */
function normalizeActiveIngredients(payload) {
  // Missing or malformed lists default to empty so headers fall back cleanly.
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload
    .map((item) => {
      // Substance name is the required label; class may be blank for null-class rows.
      const substance = toDisplayString(readFirst(item, ['SubstanceName', 'substanceName']));
      const pharmClass = toDisplayString(readFirst(item, ['PharmClassName', 'pharmClassName']));
      const unii = toDisplayString(readFirst(item, ['UNII', 'unii']));

      // Entries without a usable substance label are dropped.
      if (!substance) {
        return null;
      }

      return { substance, pharmClass, unii };
    })
    .filter(Boolean);
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
    activeIngredients: normalizeActiveIngredients(readFirst(dto, ['ActiveIngredients', 'activeIngredients'])),
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

/**************************************************************/
/**
 * Normalizes one reverse-lookup match from the API.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API reverse-lookup match DTO.
 * @returns {object | null} Reverse-lookup match view model or null.
 */
function normalizeReverseLookupMatch(dto) {
  // Missing matches cannot render a drug/signal result.
  if (!dto) {
    return null;
  }

  const drug = normalizeProduct(readFirst(dto, ['Drug', 'drug']));
  const signal = normalizeSignal(readFirst(dto, ['Signal', 'signal']));

  // Both sides are required for the row to be meaningful.
  if (!drug || !signal) {
    return null;
  }

  return {
    drug,
    signal,
    verdict: normalizeReverseLookupVerdict(readFirst(dto, ['Verdict', 'verdict'])),
  };
}

/**************************************************************/
/**
 * Builds a stable reverse-lookup de-duplication key.
 *
 * @param {object} match - Normalized reverse-lookup match.
 * @returns {string} Product and signal identity key.
 */
function getReverseLookupMatchKey(match) {
  const drugKey = match.drug?.documentGuid || match.drug?.name || '';
  const signalKey = match.signal?.name || match.signal?.id || '';

  return `${drugKey}`.trim().toLowerCase() + '|' + `${signalKey}`.trim().toLowerCase();
}

/**************************************************************/
/**
 * Ranks a reverse-lookup match for duplicate collapse.
 *
 * @param {object} match - Normalized reverse-lookup match.
 * @returns {number} Lower rank means a more actionable representative row.
 */
function getReverseLookupMatchRank(match) {
  // Fragile rows stay behind tighter evidence even when they have larger ratios.
  if (match.signal?.prec === 'fragile') {
    return 4;
  }

  if (match.signal?.sig && !match.signal?.prot) {
    return 1;
  }

  if (match.signal?.sig && match.signal?.prot) {
    return 2;
  }

  return 3;
}

/**************************************************************/
/**
 * Chooses the better representative when duplicate reverse-lookup rows exist.
 *
 * @param {object} candidate - Candidate normalized match.
 * @param {object} current - Current normalized match.
 * @returns {boolean} True when candidate should replace current.
 */
function isBetterReverseLookupMatch(candidate, current) {
  const candidateRank = getReverseLookupMatchRank(candidate);
  const currentRank = getReverseLookupMatchRank(current);

  if (candidateRank !== currentRank) {
    return candidateRank < currentRank;
  }

  const candidateNumberNeeded = candidate.signal?.nnh ?? candidate.signal?.nnt;
  const currentNumberNeeded = current.signal?.nnh ?? current.signal?.nnt;

  if (Number.isFinite(candidateNumberNeeded) && Number.isFinite(currentNumberNeeded)
    && candidateNumberNeeded !== currentNumberNeeded) {
    return candidateNumberNeeded < currentNumberNeeded;
  }

  const candidateRr = candidate.signal?.rr;
  const currentRr = current.signal?.rr;

  if (Number.isFinite(candidateRr) && Number.isFinite(currentRr) && candidateRr !== currentRr) {
    return candidate.signal?.prot ? candidateRr < currentRr : candidateRr > currentRr;
  }

  return false;
}

/**************************************************************/
/**
 * De-duplicates reverse-lookup matches by product and AE term.
 *
 * @param {object[]} matches - Normalized reverse-lookup matches.
 * @returns {object[]} De-duplicated matches preserving first-seen order.
 */
function dedupeReverseLookupMatches(matches) {
  const orderedKeys = [];
  const bestMatches = new Map();

  for (const match of matches) {
    const key = getReverseLookupMatchKey(match);

    if (!key || key === '|') {
      continue;
    }

    const currentMatch = bestMatches.get(key);

    if (!currentMatch) {
      orderedKeys.push(key);
      bestMatches.set(key, match);
      continue;
    }

    if (isBetterReverseLookupMatch(match, currentMatch)) {
      bestMatches.set(key, match);
    }
  }

  return orderedKeys.map((key) => bestMatches.get(key)).filter(Boolean);
}

/**************************************************************/
/**
 * Normalizes the reverse-lookup payload into product/signal matches.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API reverse-lookup DTO.
 * @returns {{ symptom: string, matches: object[], allReassuring: boolean }} Reverse-lookup view model.
 */
export function normalizeReverseLookup(payload) {
  // The controller always echoes the submitted symptom when the request succeeds.
  const symptom = toDisplayString(readFirst(payload, ['Symptom', 'symptom']));

  // Match arrays are defensive because a valid exact term can return no rows.
  const matchPayloads = readFirst(payload, ['Matches', 'matches']);
  const matches = Array.isArray(matchPayloads)
    ? matchPayloads.map((match) => normalizeReverseLookupMatch(match)).filter(Boolean)
    : [];

  return {
    symptom,
    matches: dedupeReverseLookupMatches(matches),
    allReassuring: toBoolean(readFirst(payload, ['AllReassuring', 'allReassuring'])),
  };
}

/**************************************************************/
/**
 * Merges multiple normalized reverse-lookup results into one de-duplicated view.
 *
 * @param {object[]} results - Normalized reverse-lookup results.
 * @param {string[]} symptoms - Submitted symptoms in display order.
 * @returns {{ symptom: string, symptoms: string[], matches: object[], allReassuring: boolean }} Merged result.
 */
export function mergeReverseLookupResults(results, symptoms) {
  const usedSymptoms = new Set();
  const selectedSymptoms = [];

  if (Array.isArray(symptoms)) {
    for (const symptom of symptoms) {
      const displaySymptom = toDisplayString(symptom);
      const lookupKey = displaySymptom.toLowerCase();

      if (!displaySymptom || usedSymptoms.has(lookupKey)) {
        continue;
      }

      usedSymptoms.add(lookupKey);
      selectedSymptoms.push(displaySymptom);
    }
  }

  const resultList = Array.isArray(results) ? results.filter(Boolean) : [];
  const matches = dedupeReverseLookupMatches(resultList.flatMap((result) => result.matches ?? []));

  return {
    symptom: selectedSymptoms.join(', '),
    symptoms: selectedSymptoms,
    matches,
    allReassuring: matches.length > 0
      && matches.every((match) => match.verdict !== 'plausiblycausal'),
  };
}

/**************************************************************/
/**
 * Normalizes one interchange row from the API.
 *
 * @param {Record<string, unknown> | null | undefined} dto - API interchange row DTO.
 * @returns {object | null} Interchange row view model or null.
 */
function normalizeInterchangeRow(dto) {
  // Missing rows cannot render a comparison.
  if (!dto) {
    return null;
  }

  const parameterName = toDisplayString(readFirst(dto, ['ParameterName', 'parameterName']));

  // Rows without an AE term cannot be explained to the user.
  if (!parameterName) {
    return null;
  }

  return {
    id: parameterName.toLowerCase(),
    parameterName,
    parameterCategory: toDisplayString(readFirst(dto, ['ParameterCategory', 'parameterCategory'])),
    signalA: normalizeSignal(readFirst(dto, ['SignalA', 'signalA'])),
    signalB: normalizeSignal(readFirst(dto, ['SignalB', 'signalB'])),
    classification: normalizeInterchangeClassification(readFirst(dto, ['Classification', 'classification'])),
    deltaLabel: toDisplayString(readFirst(dto, ['DeltaLabel', 'deltaLabel']), 'Similar signal profile'),
  };
}

/**************************************************************/
/**
 * Normalizes the interchange comparison payload.
 *
 * @param {Record<string, unknown> | null | undefined} payload - API interchange DTO.
 * @returns {object} Interchange comparison view model.
 */
export function normalizeInterchange(payload) {
  // Compared products carry full product context from the API.
  const productA = normalizeProduct(readFirst(payload, ['ProductA', 'productA']));
  const productB = normalizeProduct(readFirst(payload, ['ProductB', 'productB']));

  // Rows are grouped by classification in the component, preserving API order within groups.
  const rowPayloads = readFirst(payload, ['Rows', 'rows']);
  const rows = Array.isArray(rowPayloads)
    ? rowPayloads.map((row) => normalizeInterchangeRow(row)).filter(Boolean)
    : [];

  return {
    productA,
    productB,
    rows,
    onlyACount: toNullableNumber(readFirst(payload, ['OnlyACount', 'onlyACount'])) ?? 0,
    onlyBCount: toNullableNumber(readFirst(payload, ['OnlyBCount', 'onlyBCount'])) ?? 0,
    similarCount: toNullableNumber(readFirst(payload, ['SimilarCount', 'similarCount'])) ?? 0,
    aWorseCount: toNullableNumber(readFirst(payload, ['AWorseCount', 'aWorseCount'])) ?? 0,
    bWorseCount: toNullableNumber(readFirst(payload, ['BWorseCount', 'bWorseCount'])) ?? 0,
    classMismatchWarning: toDisplayString(
      readFirst(payload, ['ClassMismatchWarning', 'classMismatchWarning']),
    ),
    comparatorMismatchWarning: toDisplayString(
      readFirst(payload, ['ComparatorMismatchWarning', 'comparatorMismatchWarning']),
    ),
  };
}
