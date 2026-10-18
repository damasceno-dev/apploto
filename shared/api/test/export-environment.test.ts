// Review round 1 (S06) finding 3: the export's child build never inherits server configuration,
// whatever the casing or alias .NET would accept.
import { describe, expect, it } from 'vitest';
import { childEnvironment, isServerConfiguration } from '../scripts/export-openapi.ts';

describe('export child environment', () => {
  it.each([
    'ConnectionStrings__DefaultConnection',
    'connectionstrings__DefaultConnection',
    'CONNECTIONSTRINGS__DEFAULTCONNECTION',
    'ConnectionStrings:DefaultConnection',
    'Token__SigningKey',
    'TOKEN__SigningKey',
    'token:signingkey',
    'ASPNETCORE_ENVIRONMENT',
    'aspnetcore_environment',
    'DOTNET_ENVIRONMENT',
    'dotnet_environment',
    'DOTNET_ConnectionStrings__DefaultConnection',
    'aspnetcore_token__signingkey',
    'SQLCONNSTR_DefaultConnection',
    'postgresqlconnstr_DefaultConnection',
    'CustomConnStr_DefaultConnection',
  ])('treats %s as server configuration', (name) => {
    expect(isServerConfiguration(name)).toBe(true);
  });

  it.each([
    'PATH',
    'HOME',
    'DOTNET_ROOT',
    'DOTNET_CLI_TELEMETRY_OPTOUT',
    'TOKENS__X',
    'NUGET_PACKAGES',
  ])('keeps %s', (name) => {
    expect(isServerConfiguration(name)).toBe(false);
  });

  it('passes only the canonical inert values to the child', () => {
    const child = childEnvironment({
      PATH: '/usr/bin',
      connectionstrings__DefaultConnection: 'Host=real-db.example;Password=REAL',
      TOKEN__SigningKey: 'REAL-SECRET',
      'Token:ExpirationTimeInMinutes': '999',
      aspnetcore_environment: 'Development',
      SQLCONNSTR_DefaultConnection: 'Host=real-db.example',
    });
    expect(child).toEqual({
      PATH: '/usr/bin',
      ConnectionStrings__DefaultConnection:
        'Host=openapi-export.invalid;Database=openapi_export;Username=openapi_export;Password=openapi_export',
      Token__SigningKey: 'openapi-export-placeholder-signing-key-not-a-secret-0123456789',
      Token__ExpirationTimeInMinutes: '60',
    });
    expect(JSON.stringify(child)).not.toMatch(/REAL|real-db|Development|999/);
  });
});
