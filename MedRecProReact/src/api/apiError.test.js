import { describe, expect, it } from 'vitest';
import { readErrorPayload } from './apiError';

describe('readErrorPayload', () => {
  it('parses RFC 7807 problem details instead of returning the raw JSON payload', async () => {
    const problemDetails = {
      title: 'An unexpected error occurred.',
      status: 500,
      traceId: 'test-trace-id',
    };
    const response = {
      status: 500,
      headers: new Headers({ 'content-type': 'application/problem+json; charset=utf-8' }),
      json: async () => problemDetails,
      text: async () => JSON.stringify(problemDetails),
    };

    await expect(readErrorPayload(response)).resolves.toEqual({
      message: 'An unexpected error occurred.',
      details: problemDetails,
    });
  });
});
