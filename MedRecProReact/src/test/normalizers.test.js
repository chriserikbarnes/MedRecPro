import { describe, expect, it } from 'vitest';
import {
  mergeReverseLookupResults,
  normalizeInterchange,
  normalizeProduct,
  normalizeReverseLookup,
  normalizeSignal,
} from '../lib/normalizers';

const documentGuidA = '11111111-1111-1111-1111-111111111111';
const documentGuidB = '22222222-2222-2222-2222-222222222222';

function productDto(overrides = {}) {
  return {
    DocumentGUID: documentGuidA,
    ProductName: 'NORVEXIS',
    SubstanceName: 'norvexis sodium',
    UNII: 'ABC123',
    PharmClassName: 'Platelet Aggregation Inhibitor [EPC]',
    Score: 82,
    IsFavorite: true,
    ...overrides,
  };
}

function signalDto(overrides = {}) {
  return {
    EncryptedFlattenedAdverseEventRiskTableID: 'encrypted-row-id',
    ParameterName: 'Headache',
    ParameterCategory: 'Nervous System',
    RR: 1.8,
    RRLowerBound: 1.2,
    RRUpperBound: 2.7,
    NumberNeededKind: 'NNH',
    NumberNeeded: 24,
    EventsTreatment: 12,
    EventsComparator: 4,
    ArmN: 250,
    ComparatorN: 240,
    IsPlaceboControlled: true,
    IsSignificant: true,
    IsProtective: false,
    PrecisionClass: 'Tight',
    RiskSignificance: 'Elevated',
    Flags: ['LowEventCount'],
    ...overrides,
  };
}

describe('normalizers', () => {
  it('normalizes product casing variants and active ingredients', () => {
    const product = normalizeProduct({
      documentGUID: documentGuidA,
      productName: 'KAVROLIDE',
      substanceName: 'kavrolide',
      unii: 'XYZ789',
      pharmClassName: '',
      pharmClassCode: 'N0000175722',
      activeIngredients: [
        { substanceName: 'kavrolide', pharmClassName: 'Macrolide Antibacterial [EPC]', unii: 'XYZ789' },
      ],
      score: '91',
      isFavorite: 'false',
    });

    expect(product).toMatchObject({
      documentGuid: documentGuidA,
      name: 'KAVROLIDE',
      pharmClass: 'N0000175722',
      score: 91,
      isFavorite: false,
    });
    expect(product.activeIngredients).toEqual([
      {
        substance: 'kavrolide',
        pharmClass: 'Macrolide Antibacterial [EPC]',
        unii: 'XYZ789',
      },
    ]);
  });

  it('maps elevated NNH and protective NNT signals without raw integer IDs', () => {
    const nnh = normalizeSignal(signalDto());
    const nnt = normalizeSignal(signalDto({
      EncryptedFlattenedAdverseEventRiskTableID: 'encrypted-protective-id',
      NumberNeededKind: 'NNT',
      IsProtective: true,
      RiskSignificance: 'Protective',
    }));

    expect(nnh).toMatchObject({
      id: 'encrypted-row-id',
      name: 'Headache',
      type: 'NNH',
      nnh: 24,
      nnt: null,
      riskSignificance: 'Elevated',
      prec: 'tight',
    });
    expect(nnt).toMatchObject({
      id: 'encrypted-protective-id',
      type: 'NNT',
      nnh: null,
      nnt: 24,
      riskSignificance: 'Protective',
    });
    expect(nnh).not.toHaveProperty('FlattenedAdverseEventRiskTableID');
  });

  it('normalizes reverse lookup matches from product and signal DTOs', () => {
    const result = normalizeReverseLookup({
      symptom: 'Headache',
      allReassuring: false,
      matches: [
        {
          drug: productDto(),
          signal: signalDto(),
          verdict: 0,
        },
        {
          drug: productDto(),
          signal: signalDto({ RR: 1.4, NumberNeeded: 40 }),
          verdict: 'PlausiblyCausal',
        },
      ],
    });

    expect(result.symptom).toBe('Headache');
    expect(result.allReassuring).toBe(false);
    expect(result.matches).toHaveLength(1);
    expect(result.matches[0].drug.name).toBe('NORVEXIS');
    expect(result.matches[0].signal.name).toBe('Headache');
    expect(result.matches[0].signal.rr).toBe(1.8);
    expect(result.matches[0].verdict).toBe('plausiblycausal');
  });

  it('merges multi-term reverse lookup results with de-duplicated matches', () => {
    const headacheResult = normalizeReverseLookup({
      symptom: 'Headache',
      allReassuring: false,
      matches: [
        {
          drug: productDto(),
          signal: signalDto({ ParameterName: 'Headache', RR: 1.8 }),
          verdict: 'PlausiblyCausal',
        },
      ],
    });
    const nauseaResult = normalizeReverseLookup({
      symptom: 'Nausea',
      allReassuring: true,
      matches: [
        {
          drug: productDto(),
          signal: signalDto({ ParameterName: 'Headache', RR: 2.1, NumberNeeded: 18 }),
          verdict: 'PlausiblyCausal',
        },
        {
          drug: productDto({ DocumentGUID: documentGuidB, ProductName: 'OLEMITRA' }),
          signal: signalDto({ ParameterName: 'Nausea', RR: 1.1, IsSignificant: false }),
          verdict: 'NotSignificantlyElevated',
        },
      ],
    });

    const result = mergeReverseLookupResults(
      [headacheResult, nauseaResult],
      ['Headache', 'Nausea', 'headache'],
    );

    expect(result.symptom).toBe('Headache, Nausea');
    expect(result.symptoms).toEqual(['Headache', 'Nausea']);
    expect(result.matches).toHaveLength(2);
    expect(result.matches[0].signal.rr).toBe(2.1);
    expect(result.allReassuring).toBe(false);
  });

  it('normalizes interchange rows, counts, and warning text', () => {
    const comparison = normalizeInterchange({
      productA: productDto(),
      productB: productDto({ DocumentGUID: documentGuidB, ProductName: 'OLEMITRA' }),
      rows: [
        {
          parameterName: 'Nausea',
          parameterCategory: 'Gastrointestinal',
          signalA: signalDto({ ParameterName: 'Nausea', RR: 2.2 }),
          signalB: signalDto({ ParameterName: 'Nausea', RR: 1.1, IsSignificant: false }),
          classification: 'AWorse',
          deltaLabel: 'Higher RR on product A',
        },
        {
          parameterName: 'Dysesthesia',
          parameterCategory: 'Nervous System',
          signalA: null,
          signalB: signalDto({ ParameterName: 'Dysesthesia', RR: 1.8 }),
          classification: 1,
          deltaLabel: 'Only product B has this signal',
        },
      ],
      onlyACount: 1,
      onlyBCount: 0,
      similarCount: 3,
      aWorseCount: 2,
      bWorseCount: 0,
      classMismatchWarning: 'Different EPC classes.',
    });

    expect(comparison.productA.documentGuid).toBe(documentGuidA);
    expect(comparison.productB.name).toBe('OLEMITRA');
    expect(comparison.rows[0]).toMatchObject({
      parameterName: 'Nausea',
      classification: 'aworse',
      deltaLabel: 'Higher RR on product A',
    });
    expect(comparison.rows[1]).toMatchObject({
      parameterName: 'Dysesthesia',
      classification: 'onlyb',
      deltaLabel: 'Only product B has this signal',
    });
    expect(comparison.aWorseCount).toBe(2);
    expect(comparison.classMismatchWarning).toBe('Different EPC classes.');
  });
});
