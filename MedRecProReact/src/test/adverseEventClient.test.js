import { afterEach, describe, expect, it } from 'vitest';
import { AdverseEventClient, buildAdverseEventUrl } from '../api/adverseEventClient';

const originalWindow = globalThis.window;
const originalFetch = globalThis.fetch;

afterEach(() => {
  if (originalWindow === undefined) {
    delete globalThis.window;
  } else {
    globalThis.window = originalWindow;
  }

  globalThis.fetch = originalFetch;
});

/**************************************************************/
/**
 * Captures the next URL requested through the API client.
 *
 * @param {{ body?: unknown, headers?: Record<string, string> }} response - Mock response data.
 * @returns {{ urls: string[] }} Captured request data.
 */
function mockFetchUrls(response = {}) {
  const urls = [];
  const { body = {}, headers = {} } = response;

  globalThis.fetch = async (url) => {
    urls.push(url);

    return {
      ok: true,
      status: 200,
      headers: {
        get(name) {
          return headers[name] ?? null;
        },
      },
      json: async () => body,
    };
  };

  return { urls };
}

describe('buildAdverseEventUrl', () => {
  it('uses the production API path and repeated documentGuids without a browser window', () => {
    const url = buildAdverseEventUrl('reverse-lookup', {
      symptom: 'Headache',
      documentGuids: ['11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222'],
    });

    expect(url).toBe('/api/AdverseEvent/reverse-lookup?symptom=Headache&documentGuids=11111111-1111-1111-1111-111111111111&documentGuids=22222222-2222-2222-2222-222222222222');
  });

  it('targets the HTTP API profile from the standalone Vite dev port', () => {
    globalThis.window = {
      location: {
        origin: 'http://localhost:50346',
        hostname: 'localhost',
        port: '50346',
        protocol: 'http:',
      },
    };

    const url = buildAdverseEventUrl('interchange', {
      documentGuidA: '11111111-1111-1111-1111-111111111111',
      documentGuidB: '22222222-2222-2222-2222-222222222222',
      differencesOnly: true,
      sharedSignalsOnly: true,
    });

    expect(url).toBe('http://localhost:5093/api/AdverseEvent/interchange?documentGuidA=11111111-1111-1111-1111-111111111111&documentGuidB=22222222-2222-2222-2222-222222222222&differencesOnly=true&sharedSignalsOnly=true');
  });

  it('serializes interchange comparator scopes through the API client', async () => {
    const capture = mockFetchUrls();
    const args = {
      documentGuidA: '11111111-1111-1111-1111-111111111111',
      documentGuidB: '22222222-2222-2222-2222-222222222222',
      differencesOnly: true,
      sharedSignalsOnly: true,
    };

    await AdverseEventClient.getInterchange({ ...args, comparator: 'placebo' });
    await AdverseEventClient.getInterchange({ ...args, comparator: 'active' });
    await AdverseEventClient.getInterchange({ ...args, comparator: 'all' });

    expect(capture.urls[0]).toBe('/api/AdverseEvent/interchange?documentGuidA=11111111-1111-1111-1111-111111111111&documentGuidB=22222222-2222-2222-2222-222222222222&differencesOnly=true&sharedSignalsOnly=true&comparator=Placebo');
    expect(capture.urls[1]).toBe('/api/AdverseEvent/interchange?documentGuidA=11111111-1111-1111-1111-111111111111&documentGuidB=22222222-2222-2222-2222-222222222222&differencesOnly=true&sharedSignalsOnly=true&comparator=Active');
    expect(capture.urls[2]).toBe('/api/AdverseEvent/interchange?documentGuidA=11111111-1111-1111-1111-111111111111&documentGuidB=22222222-2222-2222-2222-222222222222&differencesOnly=true&sharedSignalsOnly=true');
  });

  it('omits null, undefined, and empty-string query values', () => {
    const url = buildAdverseEventUrl('products/abc/forest', {
      comparator: null,
      includeFragile: false,
      ignored: '',
      alsoIgnored: undefined,
    });

    expect(url).toBe('/api/AdverseEvent/products/abc/forest?includeFragile=false');
  });

  it('builds correlation class picker URLs through the API client', async () => {
    const capture = mockFetchUrls();

    await AdverseEventClient.getCorrelationClasses({
      classSearch: 'kinase',
      pageNumber: 2,
      pageSize: 10,
    });

    expect(capture.urls[0]).toBe('/api/AdverseEvent/correlation/classes?classSearch=kinase&pageNumber=2&pageSize=10');
  });

  it('returns correlation class pagination metadata from response headers', async () => {
    const capture = mockFetchUrls({
      body: [{ pharmClassCode: 'N0000000001' }],
      headers: {
        'X-Total-Count': '87',
        'X-Chartable-Count': '42',
        'X-Page-Number': '2',
        'X-Page-Size': '10',
      },
    });

    const page = await AdverseEventClient.getCorrelationClasses({
      classSearch: 'kinase',
      pageNumber: 2,
      pageSize: 10,
      comparator: 'Both',
      includeNonSignificant: false,
      excludeFragile: true,
      excludeCombos: true,
      minEvents: 3,
      minDrugsPerCell: 5,
    });

    expect(capture.urls[0]).toBe('/api/AdverseEvent/correlation/classes?classSearch=kinase&pageNumber=2&pageSize=10&comparator=Both&includeNonSignificant=false&excludeFragile=true&excludeCombos=true&minEvents=3&minDrugsPerCell=5');
    expect(page.items).toEqual([{ pharmClassCode: 'N0000000001' }]);
    expect(page.totalCount).toBe(87);
    expect(page.chartableCount).toBe(42);
    expect(page.pageNumber).toBe(2);
    expect(page.pageSize).toBe(10);
  });

  it('serializes every correlation-map filter through the API client', async () => {
    const capture = mockFetchUrls();

    await AdverseEventClient.getCorrelationMap({
      pharmClassCode: 'N0000175076',
      comparator: 'Both',
      includeNonSignificant: false,
      excludeFragile: true,
      minDrugsPerCell: 5,
      method: 'Pearson',
      aggregation: 'MeanLogRr',
      seriousSocOnly: true,
      excludeCombos: true,
      minEvents: 3,
    });

    expect(capture.urls[0]).toBe('/api/AdverseEvent/correlation?pharmClassCode=N0000175076&comparator=Both&includeNonSignificant=false&excludeFragile=true&minDrugsPerCell=5&method=Pearson&aggregation=MeanLogRr&seriousSocOnly=true&excludeCombos=true&minEvents=3');
  });

  it('serializes heatmap and cell-detail correlation URLs', async () => {
    const capture = mockFetchUrls();

    await AdverseEventClient.getCorrelationHeatmap({
      pharmClassCode: 'N0000175076',
      comparator: 'Active',
      minEvents: 2,
    });
    await AdverseEventClient.getCorrelationCell({
      pharmClassCode: 'N0000175076',
      socX: 'Cardiac Disorders',
      socY: 'Vascular Disorders',
      comparator: 'Placebo',
      minDrugsPerCell: 4,
    });

    expect(capture.urls[0]).toBe('/api/AdverseEvent/correlation/heatmap?pharmClassCode=N0000175076&comparator=Active&includeNonSignificant=true&excludeFragile=true&aggregation=MedianLogRr&seriousSocOnly=false&excludeCombos=false&minEvents=2');
    expect(capture.urls[1]).toBe('/api/AdverseEvent/correlation/cell?pharmClassCode=N0000175076&socX=Cardiac+Disorders&socY=Vascular+Disorders&comparator=Placebo&includeNonSignificant=true&excludeFragile=true&minDrugsPerCell=4&method=Spearman&aggregation=MedianLogRr&seriousSocOnly=false&excludeCombos=false&minEvents=0');
  });

  it('builds system picker URLs through the API client', async () => {
    const capture = mockFetchUrls({
      body: [{ systemOrganClass: 'Cardiac Disorders' }],
      headers: {
        'X-Total-Count': '17',
        'X-Chartable-Count': '9',
        'X-Page-Number': '2',
        'X-Page-Size': '5',
      },
    });

    const page = await AdverseEventClient.getCorrelationSystems({
      systemSearch: 'card',
      pageNumber: 2,
      pageSize: 5,
      comparator: 'Both',
      includeNonSignificant: false,
      excludeFragile: true,
      excludeCombos: true,
      minEvents: 4,
      minTermsPerCell: 6,
    });

    expect(capture.urls[0]).toBe('/api/AdverseEvent/correlation/systems?systemSearch=card&pageNumber=2&pageSize=5&comparator=Both&includeNonSignificant=false&excludeFragile=true&excludeCombos=true&minEvents=4&minTermsPerCell=6');
    expect(page.items).toEqual([{ systemOrganClass: 'Cardiac Disorders' }]);
    expect(page.totalCount).toBe(17);
    expect(page.chartableCount).toBe(9);
  });

  it('serializes system map filters, repeated systems, paging, and full-matrix state', async () => {
    const capture = mockFetchUrls();

    await AdverseEventClient.getSystemCorrelationMap({
      systems: ['Cardiac Disorders', 'Vascular Disorders'],
      classSearch: 'kinase',
      classPageNumber: 3,
      classPageSize: 80,
      comparator: 'Both',
      includeNonSignificant: false,
      excludeFragile: true,
      minTermsPerCell: 6,
      method: 'Pearson',
      aggregation: 'MeanLogRr',
      excludeCombos: true,
      minEvents: 5,
      includeFullMatrix: true,
    });

    expect(capture.urls[0]).toBe('/api/AdverseEvent/correlation/systems/map?systems=Cardiac+Disorders&classSearch=kinase&classPageNumber=3&classPageSize=80&comparator=Both&includeNonSignificant=false&excludeFragile=true&minTermsPerCell=6&method=Pearson&aggregation=MeanLogRr&excludeCombos=true&minEvents=5&includeFullMatrix=true');
  });

  it('serializes system heatmap class and drug paging', async () => {
    const capture = mockFetchUrls();

    await AdverseEventClient.getSystemCorrelationHeatmap({
      systems: ['Cardiac Disorders', 'Vascular Disorders'],
      classSearch: 'kinase',
      drugSearch: 'aspirin',
      classPageNumber: 2,
      classPageSize: 40,
      drugPageNumber: 4,
      drugPageSize: 100,
      comparator: 'Active',
      includeNonSignificant: true,
      excludeFragile: false,
      aggregation: 'MedianLogRr',
      excludeCombos: true,
      minEvents: 3,
    });

    expect(capture.urls[0]).toBe('/api/AdverseEvent/correlation/systems/heatmap?systems=Cardiac+Disorders&classSearch=kinase&drugSearch=aspirin&classPageNumber=2&classPageSize=40&drugPageNumber=4&drugPageSize=100&comparator=Active&includeNonSignificant=true&excludeFragile=false&aggregation=MedianLogRr&excludeCombos=true&minEvents=3');
  });

  it('serializes system cell detail term-pair paging', async () => {
    const capture = mockFetchUrls();

    await AdverseEventClient.getSystemCorrelationCell({
      systems: ['Cardiac Disorders', 'Vascular Disorders'],
      classX: 'N0000000001',
      classY: 'N0000000002',
      comparator: 'Placebo',
      includeNonSignificant: true,
      excludeFragile: true,
      minTermsPerCell: 4,
      method: 'Spearman',
      aggregation: 'MedianLogRr',
      excludeCombos: false,
      minEvents: 0,
      pageNumber: 5,
      pageSize: 250,
    });

    expect(capture.urls[0]).toBe('/api/AdverseEvent/correlation/systems/cell?systems=Cardiac+Disorders&classX=N0000000001&classY=N0000000002&comparator=Placebo&includeNonSignificant=true&excludeFragile=true&minTermsPerCell=4&method=Spearman&aggregation=MedianLogRr&excludeCombos=false&minEvents=0&pageNumber=5&pageSize=250');
  });
});
