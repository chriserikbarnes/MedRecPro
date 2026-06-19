import { describe, expect, it } from 'vitest';
import {
  mergeReverseLookupResults,
  normalizeAxisPage,
  normalizeCorrelationCellDetail,
  normalizeCorrelationClassPage,
  normalizeCorrelationClasses,
  normalizeCorrelationHeatmap,
  normalizeCorrelationMap,
  normalizeCorrelationSystem,
  normalizeCorrelationSystemPage,
  normalizeInterchange,
  normalizeProduct,
  normalizeReverseLookup,
  normalizeSignal,
  normalizeSystemClassType,
  normalizeSystemClassAxisItem,
  normalizeSystemCorrelationCellDetail,
  normalizeSystemCorrelationFilters,
  normalizeSystemCorrelationHeatmap,
  normalizeSystemCorrelationMap,
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

  it('normalizes correlation class picker rows from casing variants', () => {
    const classes = normalizeCorrelationClasses([
      {
        pharmClassCode: 'N0000000001',
        pharmClassName: 'No map class',
        encryptedPharmacologicClassId: 'enc-small',
        drugCount: '20',
        socCount: '4',
        totalOffDiagonalCellCount: 6,
        usableMapCellCount: 0,
        maxPairCount: 3,
        hasRenderableMap: false,
        renderabilityReason: 'No SOC pair meets the 4-drug floor.',
      },
      {
        PharmClassCode: 'N0000000002',
        PharmClassName: 'Kinase Inhibitor [EPC]',
        EncryptedPharmacologicClassID: 'enc-ready',
        DrugCount: 12,
        SocCount: 9,
        TotalOffDiagonalCellCount: 36,
        UsableMapCellCount: 4,
        MaxPairCount: 8,
        HasRenderableMap: true,
      },
      {
        PharmClassCode: 'N0000000003',
        PharmClassName: 'Alias Ready [EPC]',
        DrugCount: 30,
        SocCount: 3,
        IsCorrelatable: true,
      },
    ]);

    expect(classes[0]).toMatchObject({
      pharmClassCode: 'N0000000002',
      pharmClassName: 'Kinase Inhibitor [EPC]',
      encryptedPharmacologicClassId: 'enc-ready',
      drugCount: 12,
      socCount: 9,
      totalOffDiagonalCellCount: 36,
      usableMapCellCount: 4,
      maxPairCount: 8,
      hasRenderableMap: true,
      isCorrelatable: true,
    });
    expect(classes[1].pharmClassCode).toBe('N0000000003');
    expect(classes[1].hasRenderableMap).toBe(true);
    expect(classes[2]).toMatchObject({
      pharmClassCode: 'N0000000001',
      hasRenderableMap: false,
      isCorrelatable: false,
      renderabilityReason: 'No SOC pair meets the 4-drug floor.',
    });
  });

  it('normalizes correlation class pages with total counts', () => {
    const page = normalizeCorrelationClassPage({
      items: [
        {
          pharmClassCode: 'N0000000001',
          pharmClassName: 'No map class',
          hasRenderableMap: false,
        },
      ],
      totalCount: '123',
      chartableCount: '47',
      pageNumber: '1',
      pageSize: '50',
    });

    expect(page.items).toHaveLength(1);
    expect(page.totalCount).toBe(123);
    expect(page.chartableCount).toBe(47);
    expect(page.pageNumber).toBe(1);
    expect(page.pageSize).toBe(50);
  });

  it('normalizes correlation map cells without fabricating null coefficients', () => {
    const map = normalizeCorrelationMap({
      PharmClassCode: 'N0000175076',
      PharmClassName: 'Antiplatelet [EPC]',
      AppliedFilters: {
        Comparator: 2,
        IncludeNonSignificant: true,
        ExcludeFragile: true,
        MinDrugsPerCell: 4,
        Method: 'Spearman',
        Aggregation: 'MedianLogRr',
      },
      DrugCount: 8,
      Soc: ['Cardiac Disorders', 'Vascular Disorders'],
      Cells: [
        {
          RowIndex: 0,
          ColumnIndex: 0,
          RowSoc: 'Cardiac Disorders',
          ColumnSoc: 'Cardiac Disorders',
          Coefficient: 1,
          PairCount: 8,
          IsDiagonal: true,
        },
        {
          RowIndex: 0,
          ColumnIndex: 1,
          RowSoc: 'Cardiac Disorders',
          ColumnSoc: 'Vascular Disorders',
          Coefficient: null,
          PairCount: 2,
          InsufficientN: true,
          PValue: null,
        },
      ],
      Warnings: ['Below the minimum floor.'],
    });

    expect(map.appliedFilters.comparator).toBe('Both');
    expect(map.cells[1]).toMatchObject({
      coefficient: null,
      pairCount: 2,
      insufficientN: true,
    });
    expect(map.warnings).toEqual(['Below the minimum floor.']);
  });

  it('normalizes sparse heatmap cells and enum-ish precision values', () => {
    const heatmap = normalizeCorrelationHeatmap({
      pharmClassCode: 'N0000175076',
      pharmClassName: 'Antiplatelet [EPC]',
      appliedFilters: { comparator: 'Active', aggregation: 1 },
      drugCount: 2,
      soc: ['Cardiac Disorders', 'Vascular Disorders'],
      drugs: [
        { encryptedActiveMoietyId: 'enc-a', drugDisplayName: 'Drug A', documentGuid: documentGuidA },
        { encryptedActiveMoietyId: 'enc-b', drugDisplayName: 'Drug B', documentGuid: documentGuidB },
      ],
      cells: [
        {
          socIndex: 0,
          drugIndex: 1,
          logRr: '0.75',
          rr: '2.12',
          precision: 2,
          significance: 1,
          termCount: 3,
        },
      ],
    });

    expect(heatmap.appliedFilters.aggregation).toBe('MeanLogRr');
    expect(heatmap.cells).toHaveLength(1);
    expect(heatmap.cells[0]).toMatchObject({
      socIndex: 0,
      drugIndex: 1,
      logRr: 0.75,
      precision: 'fragile',
      significance: 'elevated',
    });
  });

  it('normalizes cell detail map-safe and raw diagnostic coefficients separately', () => {
    const detail = normalizeCorrelationCellDetail({
      PharmClassCode: 'N0000175076',
      PharmClassName: 'Antiplatelet [EPC]',
      SocX: 'Cardiac Disorders',
      SocY: 'Vascular Disorders',
      Coefficient: null,
      RawCoefficient: 0.91,
      PValue: null,
      RawPValue: 0.04,
      InsufficientN: true,
      MinDrugsPerCell: 4,
      PairCount: 3,
      DrugPairs: [
        {
          DrugDisplayName: 'Drug A',
          EncryptedActiveMoietyID: 'enc-a',
          LogRrX: 0.4,
          LogRrY: 0.8,
          RrX: 1.49,
          RrY: 2.22,
          PrecisionX: 'Tight',
          PrecisionY: 'Wide',
          TermCountX: 2,
          TermCountY: 4,
        },
      ],
      Warnings: ['Raw value shown for diagnostics only.'],
    });

    expect(detail.coefficient).toBeNull();
    expect(detail.rawCoefficient).toBe(0.91);
    expect(detail.drugPairs[0]).toMatchObject({
      drugDisplayName: 'Drug A',
      precisionX: 'tight',
      precisionY: 'wide',
      termCountY: 4,
    });
    expect(detail.warnings).toEqual(['Raw value shown for diagnostics only.']);
  });

  it('normalizes system correlation filters and axis pages from enum values', () => {
    const filters = normalizeSystemCorrelationFilters({
      Comparator: 2,
      IncludeNonSignificant: 'false',
      ExcludeFragile: 'true',
      MinTermsPerCell: '6',
      Method: 1,
      Aggregation: 1,
      ExcludeCombos: true,
      MinEvents: '3',
    });
    const page = normalizeAxisPage({
      PageNumber: 2,
      PageSize: 40,
      TotalCount: 101,
      TotalPages: 3,
      HasPreviousPage: true,
      HasNextPage: true,
    });

    expect(filters).toMatchObject({
      comparator: 'Both',
      includeNonSignificant: false,
      excludeFragile: true,
      minTermsPerCell: 6,
      method: 'Pearson',
      aggregation: 'MeanLogRr',
      excludeCombos: true,
      minEvents: 3,
    });
    expect(page).toEqual({
      pageNumber: 2,
      pageSize: 40,
      totalCount: 101,
      totalPages: 3,
      hasPreviousPage: true,
      hasNextPage: true,
    });
  });

  it('normalizes system picker rows and pages from casing variants', () => {
    const row = normalizeCorrelationSystem({
      systemOrganClass: 'Cardiac Disorders',
      classCount: '12',
      drugCount: '91',
      termCount: '44',
      usableMapCellCount: 5,
      maxPairCount: 8,
      hasRenderableMap: 'true',
    });
    const page = normalizeCorrelationSystemPage({
      items: [
        {
          SystemOrganClass: 'Vascular Disorders',
          ClassCount: 10,
          DrugCount: 80,
          TermCount: 30,
          HasRenderableMap: false,
          RenderabilityReason: 'No class-pair cells meet the term floor.',
        },
      ],
      TotalCount: '17',
      ChartableCount: '9',
      PageNumber: '1',
      PageSize: '50',
    });

    expect(row).toMatchObject({
      systemOrganClass: 'Cardiac Disorders',
      classCount: 12,
      drugCount: 91,
      termCount: 44,
      usableMapCellCount: 5,
      maxPairCount: 8,
      hasRenderableMap: true,
    });
    expect(page.items[0]).toMatchObject({
      systemOrganClass: 'Vascular Disorders',
      hasRenderableMap: false,
      renderabilityReason: 'No class-pair cells meet the term floor.',
    });
    expect(page.totalCount).toBe(17);
    expect(page.chartableCount).toBe(9);
  });

  it('normalizes system class axis items without raw integer IDs', () => {
    const axis = normalizeSystemClassAxisItem({
      Index: 3,
      PharmClassCode: 'N0000000001',
      PharmClassName: 'Kinase Inhibitor [EPC]',
      EncryptedPharmacologicClassID: 'enc-class',
      TermCount: 12,
      DrugCount: 40,
      HasRenderableMap: true,
    });

    expect(axis).toMatchObject({
      index: 3,
      pharmClassCode: 'N0000000001',
      pharmClassName: 'Kinase Inhibitor [EPC]',
      classType: 'EPC',
      encryptedPharmacologicClassId: 'enc-class',
      termCount: 12,
      drugCount: 40,
      hasRenderableMap: true,
    });
    expect(axis).not.toHaveProperty('PharmacologicClassID');
  });

  it('normalizes system class type tokens and bracketed input', () => {
    expect(normalizeSystemClassType('[ep]')).toBe('EP');
    expect(normalizeSystemClassType('MoA')).toBe('MOA');
    expect(normalizeSystemClassType('other')).toBe('Other');
    expect(normalizeSystemClassType('', 'All')).toBe('All');
  });

  it('normalizes system map cells with null coefficients and full-matrix metadata', () => {
    const map = normalizeSystemCorrelationMap({
      SelectedSystems: ['Cardiac Disorders', 'Vascular Disorders'],
      AppliedFilters: { Comparator: 'Both', MinTermsPerCell: 6, Method: 'Pearson' },
      ClassCount: 83,
      IncludesFullMatrix: true,
      SelectedClassType: '[epc]',
      ClassTypeFacets: [
        { ClassType: 'EPC', DisplayLabel: 'EPC', ClassCount: 50, HasRenderableMap: true },
        { ClassType: 'MOA', DisplayLabel: 'MOA', ClassCount: 33, HasRenderableMap: false },
      ],
      ClassPage: {
        PageNumber: 1,
        PageSize: 40,
        TotalCount: 83,
        TotalPages: 3,
        HasPreviousPage: false,
        HasNextPage: true,
      },
      Classes: [
        { Index: 0, PharmClassCode: 'N0000000001', PharmClassName: 'Class A', TermCount: 10 },
        { Index: 1, PharmClassCode: 'N0000000002', PharmClassName: 'Class B', TermCount: 9 },
      ],
      Cells: [
        {
          RowIndex: 0,
          ColumnIndex: 1,
          RowClassCode: 'N0000000001',
          ColumnClassCode: 'N0000000002',
          Coefficient: null,
          PairCount: 2,
          InsufficientN: true,
          PValue: null,
        },
      ],
      ClassSummaries: [
        { Index: 0, PharmClassCode: 'N0000000001', PharmClassName: 'Class A [EPC]', DrugCount: 11, TermCount: 4 },
      ],
      Warnings: ['Below floor.'],
    });

    expect(map.selectedSystems).toEqual(['Cardiac Disorders', 'Vascular Disorders']);
    expect(map.includesFullMatrix).toBe(true);
    expect(map.selectedClassType).toBe('EPC');
    expect(map.classTypeFacets.map((facet) => facet.classType)).toEqual(['All', 'EPC', 'MOA']);
    expect(map.classTypeFacets[0].classCount).toBe(83);
    expect(map.classPage.totalCount).toBe(83);
    expect(map.cells[0]).toMatchObject({
      coefficient: null,
      pairCount: 2,
      insufficientN: true,
      rowClassCode: 'N0000000001',
      columnClassCode: 'N0000000002',
    });
    expect(map.classSummaries[0].drugCount).toBe(11);
    expect(map.classSummaries[0].classType).toBe('EPC');
    expect(map.warnings).toEqual(['Below floor.']);
  });

  it('derives fallback class-type facets when the API omits server facets', () => {
    const map = normalizeSystemCorrelationMap({
      Classes: [
        { Index: 0, PharmClassCode: 'EPC-A', PharmClassName: 'Class A [EPC]', HasRenderableMap: true },
        { Index: 1, PharmClassCode: 'MOA-A', PharmClassName: 'Class B [MoA]', HasRenderableMap: false },
      ],
    });

    expect(map.classTypeFacets.map((facet) => facet.classType)).toEqual(['All', 'EPC', 'MOA']);
    expect(map.classes.map((item) => item.classType)).toEqual(['EPC', 'MOA']);
  });

  it('normalizes system sparse heatmap cells with class and drug pages', () => {
    const heatmap = normalizeSystemCorrelationHeatmap({
      selectedSystems: ['Cardiac Disorders'],
      appliedFilters: { comparator: 'Active', aggregation: 1 },
      selectedClassType: 'MOA',
      classTypeFacets: [
        { classType: 'EPC', displayLabel: 'EPC', classCount: 1, hasRenderableMap: false },
        { classType: 'MOA', displayLabel: 'MOA', classCount: 2, hasRenderableMap: true },
      ],
      classPage: { pageNumber: 2, pageSize: 40, totalCount: 80, totalPages: 2, hasPreviousPage: true },
      drugPage: { pageNumber: 3, pageSize: 50, totalCount: 160, totalPages: 4, hasNextPage: true },
      classes: [
        { index: 4, pharmClassCode: 'N0000000004', pharmClassName: 'Class D', termCount: 7 },
      ],
      drugs: [
        { index: 8, encryptedActiveMoietyId: 'enc-drug', drugDisplayName: 'Drug A', documentGuid: documentGuidA },
      ],
      cells: [
        {
          classIndex: 4,
          drugIndex: 8,
          logRr: '0.25',
          rr: '1.28',
          precision: 0,
          significance: 2,
          termCount: 3,
        },
      ],
    });

    expect(heatmap.appliedFilters.aggregation).toBe('MeanLogRr');
    expect(heatmap.selectedClassType).toBe('MOA');
    expect(heatmap.classTypeFacets.map((facet) => facet.classType)).toEqual(['All', 'EPC', 'MOA']);
    expect(heatmap.classPage.pageNumber).toBe(2);
    expect(heatmap.drugPage.pageNumber).toBe(3);
    expect(heatmap.cells[0]).toMatchObject({
      classIndex: 4,
      drugIndex: 8,
      logRr: 0.25,
      precision: 'tight',
      significance: 'protective',
    });
  });

  it('normalizes system cell detail term-pair paging and raw coefficients', () => {
    const detail = normalizeSystemCorrelationCellDetail({
      SelectedSystems: ['Cardiac Disorders'],
      ClassX: { Index: 0, PharmClassCode: 'N0000000001', PharmClassName: 'Class A' },
      ClassY: { Index: 1, PharmClassCode: 'N0000000002', PharmClassName: 'Class B' },
      AppliedFilters: { Comparator: 0, MinTermsPerCell: 4 },
      Coefficient: null,
      RawCoefficient: 0.72,
      PValue: null,
      RawPValue: 0.03,
      PairCount: 12,
      MinTermsPerCell: 4,
      InsufficientN: true,
      TermPairPage: {
        PageNumber: 2,
        PageSize: 100,
        TotalCount: 180,
        TotalPages: 2,
        HasPreviousPage: true,
      },
      TermPairs: [
        {
          SystemOrganClass: 'Cardiac Disorders',
          ParameterName: 'Palpitations',
          LogRrX: 0.4,
          LogRrY: 0.8,
          RrX: 1.49,
          RrY: 2.22,
          PrecisionX: 'Tight',
          PrecisionY: 'Wide',
          SignificanceX: 'Elevated',
          SignificanceY: 'Protective',
          DrugCountX: 6,
          DrugCountY: 5,
          TermCountX: 2,
          TermCountY: 3,
        },
      ],
      Warnings: ['Raw value shown for diagnostics only.'],
    });

    expect(detail.coefficient).toBeNull();
    expect(detail.rawCoefficient).toBe(0.72);
    expect(detail.termPairPage).toMatchObject({ pageNumber: 2, pageSize: 100, totalCount: 180 });
    expect(detail.termPairs[0]).toMatchObject({
      systemOrganClass: 'Cardiac Disorders',
      parameterName: 'Palpitations',
      precisionX: 'tight',
      precisionY: 'wide',
      significanceX: 'elevated',
      significanceY: 'protective',
      drugCountX: 6,
    });
    expect(detail.warnings).toEqual(['Raw value shown for diagnostics only.']);
  });
});
