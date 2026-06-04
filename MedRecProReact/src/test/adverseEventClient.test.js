import { afterEach, describe, expect, it } from 'vitest';
import { buildAdverseEventUrl } from '../api/adverseEventClient';

const originalWindow = globalThis.window;

afterEach(() => {
  if (originalWindow === undefined) {
    delete globalThis.window;
    return;
  }

  globalThis.window = originalWindow;
});

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
    });

    expect(url).toBe('http://localhost:5093/api/AdverseEvent/interchange?documentGuidA=11111111-1111-1111-1111-111111111111&documentGuidB=22222222-2222-2222-2222-222222222222&differencesOnly=true');
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
});
