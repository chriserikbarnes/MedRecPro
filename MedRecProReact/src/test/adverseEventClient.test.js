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
});
