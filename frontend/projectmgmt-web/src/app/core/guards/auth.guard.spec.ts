import { describe, expect, it } from 'vitest';
import { safeReturnUrl } from './auth.guard';

describe('safeReturnUrl', () => {
  it('keeps internal paths such as invite links', () => {
    expect(safeReturnUrl('/invite/abc123')).toBe('/invite/abc123');
    expect(safeReturnUrl('/orgs/o1/projects?create=1')).toBe('/orgs/o1/projects?create=1');
  });

  it('rejects external, protocol-relative and auth paths', () => {
    expect(safeReturnUrl('https://evil.example')).toBeNull();
    expect(safeReturnUrl('//evil.example')).toBeNull();
    expect(safeReturnUrl('/\\evil.example')).toBeNull();
    expect(safeReturnUrl('/auth?x=1')).toBeNull();
    expect(safeReturnUrl(null)).toBeNull();
  });
});
