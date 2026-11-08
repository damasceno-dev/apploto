import { describe, expect, it } from 'vitest';
import { deriveOperations } from '../scripts/codegen';

const schemes = {
  IdentityBearer: { type: 'http', scheme: 'bearer' },
  BranchBearer: { type: 'http', scheme: 'bearer' },
};

describe('success representation derives from authoritative OpenAPI content', () => {
  it('marks exclusively HTML success independently of its JSON error responses', () => {
    const operations = deriveOperations({
      components: { securitySchemes: schemes },
      paths: {
        '/synthetic-print': {
          get: {
            security: [{ BranchBearer: ['Manager', 'Admin'] }],
            responses: {
              '200': { content: { 'text/html': {} } },
              '400': { content: { 'application/json': {} } },
            },
          },
        },
      },
    });
    expect(operations).toEqual([
      {
        method: 'GET',
        path: '/synthetic-print',
        authTier: 'branch',
        headers: [],
        textResponse: true,
      },
    ]);
  });
  it('refuses ambiguous mixed HTML/JSON success rather than choosing a parser by route name', () => {
    expect(() =>
      deriveOperations({
        components: { securitySchemes: schemes },
        paths: {
          '/synthetic-mixed': {
            get: {
              security: [],
              responses: { '200': { content: { 'text/html': {}, 'application/json': {} } } },
            },
          },
        },
      }),
    ).toThrow('mixes HTML and other success representations');
  });
});
