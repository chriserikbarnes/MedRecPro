/**************************************************************/
/**
 * DTO normalizers for AE dashboard API payloads.
 *
 * @fileoverview Accepts PascalCase, camelCase, and acronym casing variants, and
 * also tolerates enum values serialized as numbers by Newtonsoft.Json.
 */
/**************************************************************/

const PRECISION_BY_VALUE = { 0: 'tight', 1: 'wide', 2: 'fragile' };
const TIER_BY_VALUE = { 0: 'counsel', 1: 'watch', 2: 'reassure', 3: 'fragile' };
const SIGNIFICANCE_BY_VALUE = { 0: 'not-significant', 1: 'elevated', 2: 'protective' };
const NUMBER_KIND_BY_VALUE = { 0: 'None', 1: 'NNH', 2: 'NNT' };
const FLAG_BY_VALUE = { 0: 'ZERO_CELL_CORRECTED', 1: 'SOC_REMAP', 2: 'WIDE_CI', 3: 'LOW_EVENT_COUNT' };
const INTERCHANGE_BY_VALUE = { 0: 'only-a', 1: 'only-b', 2: 'similar', 3: 'a-worse', 4: 'b-worse' };
const VERDICT_BY_VALUE = { 0: 'plausibly-causal', 1: 'protective', 2: 'not-significantly-elevated', 3: 'low-confidence' };

/**************************************************************/
/**
 * Reads a field by trying multiple names case-insensitively.
 *
 * @param {Object} source Source object.
 * @param {Array<string>} names Candidate names.
 * @param {any} fallback Fallback value.
 * @returns {any} Field value.
 */
/**************************************************************/
export function readField(source, names, fallback = null) {
    if (!source) {
        return fallback;
    }

    for (const name of names) {
        if (Object.prototype.hasOwnProperty.call(source, name)) {
            return source[name];
        }
    }

    const lookup = new Map(Object.keys(source).map(key => [key.toLowerCase(), key]));
    for (const name of names) {
        const key = lookup.get(name.toLowerCase());
        if (key) {
            return source[key];
        }
    }

    return fallback;
}

/**************************************************************/
/**
 * Normalizes one product summary DTO.
 *
 * @param {Object} dto API DTO.
 * @returns {Object|null} Product view model.
 */
/**************************************************************/
export function normalizeProduct(dto) {
    if (!dto) {
        return null;
    }

    const documentGuid = stringOrNull(readField(dto, ['DocumentGUID', 'documentGUID', 'DocumentGuid', 'documentGuid']));
    if (!documentGuid) {
        return null;
    }

    const pharmClassName = stringOrNull(readField(dto, ['PharmClassName', 'pharmClassName']));
    const pharmClassCode = stringOrNull(readField(dto, ['PharmClassCode', 'pharmClassCode']));

    return {
        id: documentGuid,
        documentGuid,
        name: stringOrNull(readField(dto, ['ProductName', 'productName'])) || 'Unnamed product',
        generic: stringOrNull(readField(dto, ['SubstanceName', 'substanceName'])) || 'Substance not listed',
        moiety: stringOrNull(readField(dto, ['UNII', 'unii'])) || 'UNII not listed',
        pharmClass: pharmClassName || pharmClassCode || 'Class not listed',
        armN: numberOrNull(readField(dto, ['ArmN', 'armN'])),
        comparatorN: numberOrNull(readField(dto, ['ComparatorN', 'comparatorN'])),
        rowCount: numberOrZero(readField(dto, ['RowCount', 'rowCount'])),
        significant: numberOrZero(readField(dto, ['SignificantCount', 'significantCount'])),
        significantProtective: numberOrZero(readField(dto, ['SignificantProtectiveCount', 'significantProtectiveCount'])),
        significantElevated: numberOrZero(readField(dto, ['SignificantElevatedCount', 'significantElevatedCount'])),
        placeboCoverage: Boolean(readField(dto, ['PlaceboCoverage', 'placeboCoverage'], false)),
        activeCoverage: Boolean(readField(dto, ['ActiveCoverage', 'activeCoverage'], false)),
        doseCoverage: numberOrZero(readField(dto, ['DoseCoverage', 'doseCoverage'])),
        socBreadth: numberOrZero(readField(dto, ['SocBreadth', 'socBreadth'])),
        socTotal: numberOrZero(readField(dto, ['SocTotal', 'socTotal'])) || 17,
        monoComboMix: normalizeTextEnum(readField(dto, ['MonoComboMix', 'monoComboMix'])),
        score: numberOrZero(readField(dto, ['Score', 'score'])),
        scoreReason: stringOrNull(readField(dto, ['ScoreReason', 'scoreReason'])) || '',
        isFavorite: Boolean(readField(dto, ['IsFavorite', 'isFavorite'], false))
    };
}

/**************************************************************/
/**
 * Normalizes an AE risk signal DTO.
 *
 * @param {Object} dto API DTO.
 * @returns {Object} Signal view model.
 */
/**************************************************************/
export function normalizeSignal(dto) {
    if (!dto) {
        return null;
    }

    const numberKind = normalizeNumberKind(readField(dto, ['NumberNeededKind', 'numberNeededKind', 'NumberNeededType', 'numberNeededType']));
    const significance = normalizeSignificance(readField(dto, ['RiskSignificance', 'riskSignificance', 'Significance', 'significance']));
    const isProtective = readField(dto, ['IsProtective', 'isProtective'], null);
    const protective = isProtective === null ? significance === 'protective' : Boolean(isProtective);
    const isSignificant = readField(dto, ['IsSignificant', 'isSignificant'], null);
    const significant = isSignificant === null ? significance === 'elevated' || significance === 'protective' : Boolean(isSignificant);
    const numberNeeded = numberOrNull(readField(dto, ['NumberNeeded', 'numberNeeded']));
    const numberNeededLower = numberOrNull(readField(dto, ['NumberNeededLowerBound', 'numberNeededLowerBound']));
    const numberNeededUpper = numberOrNull(readField(dto, ['NumberNeededUpperBound', 'numberNeededUpperBound']));

    return {
        id: stringOrNull(readField(dto, ['EncryptedFlattenedAdverseEventRiskTableID', 'encryptedFlattenedAdverseEventRiskTableID']))
            || `${readField(dto, ['DocumentGUID', 'documentGUID', 'DocumentGuid', 'documentGuid'], '')}:${readField(dto, ['ParameterName', 'parameterName'], '')}`,
        name: stringOrNull(readField(dto, ['ParameterName', 'parameterName'])) || 'Unnamed adverse event',
        soc: stringOrNull(readField(dto, ['ParameterCategory', 'parameterCategory'])) || 'Uncategorized',
        rr: numberOrNull(readField(dto, ['RR', 'rr'])),
        rrL: numberOrNull(readField(dto, ['RRLowerBound', 'rrLowerBound'])),
        rrH: numberOrNull(readField(dto, ['RRUpperBound', 'rrUpperBound'])),
        nnh: numberKind === 'NNH' ? numberNeeded : null,
        nnhL: numberKind === 'NNH' ? numberNeededLower : null,
        nnhH: numberKind === 'NNH' ? numberNeededUpper : null,
        nnt: numberKind === 'NNT' ? numberNeeded : null,
        nntL: numberKind === 'NNT' ? numberNeededLower : null,
        nntH: numberKind === 'NNT' ? numberNeededUpper : null,
        type: numberKind,
        eT: numberOrNull(readField(dto, ['EventsTreatment', 'eventsTreatment'])),
        eC: numberOrNull(readField(dto, ['EventsComparator', 'eventsComparator'])),
        armN: numberOrNull(readField(dto, ['ArmN', 'armN'])),
        comparatorN: numberOrNull(readField(dto, ['ComparatorN', 'comparatorN'])),
        isPlac: Boolean(readField(dto, ['IsPlaceboControlled', 'isPlaceboControlled'], false)),
        combo: Boolean(readField(dto, ['IsCombo', 'isCombo'], false)),
        prec: normalizePrecision(readField(dto, ['PrecisionClass', 'precisionClass'])),
        sig: significant,
        prot: protective,
        riskSignificance: significance,
        flags: normalizeFlags(readField(dto, ['Flags', 'flags'], []), readField(dto, ['CalculationFlags', 'calculationFlags'], '')),
        tier: normalizeTierKey(readField(dto, ['CounselingTier', 'counselingTier'])),
        dose: numberOrNull(readField(dto, ['Dose', 'dose'])),
        doseUnit: stringOrNull(readField(dto, ['DoseUnit', 'doseUnit'])),
        studyContext: stringOrNull(readField(dto, ['StudyContext', 'studyContext'])),
        population: stringOrNull(readField(dto, ['Population', 'population'])),
        subpopulation: stringOrNull(readField(dto, ['Subpopulation', 'subpopulation']))
    };
}

/**************************************************************/
/**
 * Normalizes a triage payload.
 *
 * @param {Object} dto API payload.
 * @returns {Object} Triage view model.
 */
/**************************************************************/
export function normalizeTriage(dto) {
    return {
        product: normalizeProduct(readField(dto, ['Product', 'product'])),
        tiers: (readField(dto, ['Tiers', 'tiers'], []) || []).map(normalizeTier)
    };
}

/**************************************************************/
/**
 * Normalizes a forest payload.
 *
 * @param {Object} dto API payload.
 * @returns {Object} Forest view model.
 */
/**************************************************************/
export function normalizeForest(dto) {
    return {
        signals: (readField(dto, ['Signals', 'signals'], []) || []).map(normalizeSignal),
        axisTicks: (readField(dto, ['AxisTicks', 'axisTicks'], []) || []).map(Number).filter(Number.isFinite)
    };
}

/**************************************************************/
/**
 * Normalizes a quadrant payload.
 *
 * @param {Object} dto API payload.
 * @returns {Object} Quadrant view model.
 */
/**************************************************************/
export function normalizeQuadrant(dto) {
    return {
        points: (readField(dto, ['Points', 'points'], []) || []).map(point => ({
            id: stringOrNull(readField(point, ['EncryptedFlattenedAdverseEventRiskTableID', 'encryptedFlattenedAdverseEventRiskTableID'])),
            signal: normalizeSignal(readField(point, ['Signal', 'signal'])),
            precisionX: numberOrNull(readField(point, ['PrecisionX', 'precisionX'])),
            magnitudeY: numberOrNull(readField(point, ['MagnitudeY', 'magnitudeY'])),
            bubbleSize: numberOrNull(readField(point, ['BubbleSize', 'bubbleSize'])),
            direction: normalizeSignificance(readField(point, ['Direction', 'direction']))
        }))
    };
}

/**************************************************************/
/**
 * Normalizes a reverse lookup payload.
 *
 * @param {Object} dto API payload.
 * @returns {Object} Reverse lookup view model.
 */
/**************************************************************/
export function normalizeReverseLookup(dto) {
    return {
        symptom: stringOrNull(readField(dto, ['Symptom', 'symptom'])) || '',
        allReassuring: Boolean(readField(dto, ['AllReassuring', 'allReassuring'], false)),
        matches: (readField(dto, ['Matches', 'matches'], []) || []).map(match => ({
            drug: normalizeProduct(readField(match, ['Drug', 'drug'])),
            signal: normalizeSignal(readField(match, ['Signal', 'signal'])),
            verdict: normalizeVerdict(readField(match, ['Verdict', 'verdict']))
        }))
    };
}

/**************************************************************/
/**
 * Normalizes an interchange payload.
 *
 * @param {Object} dto API payload.
 * @returns {Object} Interchange view model.
 */
/**************************************************************/
export function normalizeInterchange(dto) {
    return {
        productA: normalizeProduct(readField(dto, ['ProductA', 'productA'])),
        productB: normalizeProduct(readField(dto, ['ProductB', 'productB'])),
        rows: (readField(dto, ['Rows', 'rows'], []) || []).map(row => ({
            name: stringOrNull(readField(row, ['ParameterName', 'parameterName'])) || 'Unnamed adverse event',
            soc: stringOrNull(readField(row, ['ParameterCategory', 'parameterCategory'])) || 'Uncategorized',
            signalA: normalizeSignal(readField(row, ['SignalA', 'signalA'])),
            signalB: normalizeSignal(readField(row, ['SignalB', 'signalB'])),
            classification: normalizeInterchangeClass(readField(row, ['Classification', 'classification'])),
            deltaLabel: stringOrNull(readField(row, ['DeltaLabel', 'deltaLabel'])) || ''
        })),
        onlyACount: numberOrZero(readField(dto, ['OnlyACount', 'onlyACount'])),
        onlyBCount: numberOrZero(readField(dto, ['OnlyBCount', 'onlyBCount'])),
        similarCount: numberOrZero(readField(dto, ['SimilarCount', 'similarCount'])),
        aWorseCount: numberOrZero(readField(dto, ['AWorseCount', 'aWorseCount'])),
        bWorseCount: numberOrZero(readField(dto, ['BWorseCount', 'bWorseCount'])),
        classMismatchWarning: stringOrNull(readField(dto, ['ClassMismatchWarning', 'classMismatchWarning'])),
        comparatorMismatchWarning: stringOrNull(readField(dto, ['ComparatorMismatchWarning', 'comparatorMismatchWarning']))
    };
}

/**************************************************************/
/**
 * Normalizes one counseling tier DTO.
 *
 * @param {Object} dto API tier DTO.
 * @returns {Object} Tier view model.
 */
/**************************************************************/
function normalizeTier(dto) {
    const tier = normalizeTierKey(readField(dto, ['Tier', 'tier']));
    return {
        id: tier,
        name: stringOrNull(readField(dto, ['Name', 'name'])) || defaultTierName(tier),
        description: stringOrNull(readField(dto, ['Description', 'description'])) || defaultTierDescription(tier),
        signals: (readField(dto, ['Signals', 'signals'], []) || []).map(signal => ({
            ...normalizeSignal(signal),
            tier
        }))
    };
}

function normalizePrecision(value) {
    if (Number.isInteger(value)) {
        return PRECISION_BY_VALUE[value] || 'tight';
    }

    const text = normalizeTextEnum(value).toLowerCase();
    if (text.includes('fragile')) return 'fragile';
    if (text.includes('wide')) return 'wide';
    return 'tight';
}

function normalizeTierKey(value) {
    if (Number.isInteger(value)) {
        return TIER_BY_VALUE[value] || 'reassure';
    }

    const text = normalizeTextEnum(value).toLowerCase();
    if (text.includes('counsel') || text.includes('expect')) return 'counsel';
    if (text.includes('watch')) return 'watch';
    if (text.includes('fragile') || text.includes('low')) return 'fragile';
    return 'reassure';
}

function normalizeSignificance(value) {
    if (Number.isInteger(value)) {
        return SIGNIFICANCE_BY_VALUE[value] || 'not-significant';
    }

    const text = normalizeTextEnum(value).toLowerCase();
    if (text.includes('protect')) return 'protective';
    if (text.includes('elevat')) return 'elevated';
    return 'not-significant';
}

function normalizeNumberKind(value) {
    if (Number.isInteger(value)) {
        return NUMBER_KIND_BY_VALUE[value] || 'None';
    }

    const text = normalizeTextEnum(value).toUpperCase();
    if (text.includes('NNT')) return 'NNT';
    if (text.includes('NNH')) return 'NNH';
    return 'None';
}

function normalizeFlags(flags, calculationFlags) {
    const tokens = [];

    if (Array.isArray(flags)) {
        flags.forEach(flag => {
            if (Number.isInteger(flag)) {
                tokens.push(FLAG_BY_VALUE[flag]);
            } else if (flag) {
                tokens.push(normalizeFlagName(flag));
            }
        });
    }

    if (calculationFlags) {
        String(calculationFlags)
            .split(/[;,|]+/)
            .map(flag => flag.trim())
            .filter(Boolean)
            .forEach(flag => tokens.push(normalizeFlagName(flag)));
    }

    return Array.from(new Set(tokens.filter(Boolean)));
}

function normalizeFlagName(value) {
    const text = normalizeTextEnum(value);
    return text
        .replace(/([a-z])([A-Z])/g, '$1_$2')
        .replace(/[-\s]+/g, '_')
        .toUpperCase();
}

function normalizeInterchangeClass(value) {
    if (Number.isInteger(value)) {
        return INTERCHANGE_BY_VALUE[value] || 'similar';
    }

    const text = normalizeTextEnum(value).toLowerCase();
    if (text.includes('onlya') || text.includes('only a')) return 'only-a';
    if (text.includes('onlyb') || text.includes('only b')) return 'only-b';
    if (text.includes('aworse') || text.includes('a worse')) return 'a-worse';
    if (text.includes('bworse') || text.includes('b worse')) return 'b-worse';
    return 'similar';
}

function normalizeVerdict(value) {
    if (Number.isInteger(value)) {
        return VERDICT_BY_VALUE[value] || 'not-significantly-elevated';
    }

    const text = normalizeTextEnum(value).toLowerCase();
    if (text.includes('causal')) return 'plausibly-causal';
    if (text.includes('protect')) return 'protective';
    if (text.includes('low')) return 'low-confidence';
    return 'not-significantly-elevated';
}

function normalizeTextEnum(value) {
    if (value === null || value === undefined) {
        return '';
    }

    return String(value).trim();
}

function numberOrNull(value) {
    const number = Number(value);
    return Number.isFinite(number) ? number : null;
}

function numberOrZero(value) {
    const number = Number(value);
    return Number.isFinite(number) ? number : 0;
}

function stringOrNull(value) {
    return value === null || value === undefined || value === '' ? null : String(value);
}

function defaultTierName(tier) {
    return {
        counsel: 'Expect and counsel',
        watch: 'Watch - rare but serious',
        reassure: 'Reassure',
        fragile: 'Low confidence - interpret with care'
    }[tier] || 'Signals';
}

function defaultTierDescription(tier) {
    return {
        counsel: 'Common, tight-precision signals to mention to the patient up front.',
        watch: 'Lower-probability signals in serious organ systems with red-flag instructions.',
        reassure: 'Not significantly elevated, or significantly protective.',
        fragile: 'Data-quality flags or extreme bounds. Do not drive counseling from these alone.'
    }[tier] || '';
}
