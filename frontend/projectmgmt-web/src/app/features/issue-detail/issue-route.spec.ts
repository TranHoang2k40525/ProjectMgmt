import { describe, expect, it } from 'vitest';
import { resolveIssueRoute } from './issue-route';

describe('resolveIssueRoute', () => {
  const items = [{ id: 'c0de0000-0000-0000-0007-000000000002', issueKey: 'DEMO-2' }];

  it('nhận GUID trực tiếp', () => {
    expect(resolveIssueRoute('C0DE0000-0000-0000-0007-000000000002', [])).toEqual({ kind: 'id', id: 'C0DE0000-0000-0000-0007-000000000002' });
  });

  it('phân giải mã Issue trong Project đang chọn, không phân biệt hoa thường', () => {
    expect(resolveIssueRoute('demo-2', items)).toEqual({ kind: 'key', key: 'DEMO-2', id: items[0].id });
    expect(resolveIssueRoute('DEMO-99', items)).toEqual({ kind: 'key', key: 'DEMO-99', id: null });
  });

  it('từ chối đường dẫn không phải GUID hay mã Issue', () => {
    expect(resolveIssueRoute('abc', items)).toEqual({ kind: 'invalid' });
    expect(resolveIssueRoute('DEMO-', items)).toEqual({ kind: 'invalid' });
  });
});
