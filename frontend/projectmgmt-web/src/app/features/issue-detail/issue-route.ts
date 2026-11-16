const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const ISSUE_KEY = /^[A-Za-z][A-Za-z0-9]{1,9}-\d+$/;

export type IssueRouteTarget =
  | { kind: 'id'; id: string }
  | { kind: 'key'; key: string; id: string | null }
  | { kind: 'invalid' };

/**
 * /issues/:id nhận GUID hoặc mã dạng DEMO-12. Mã chỉ phân giải được trong Project đang chọn
 * (ProjectKey chỉ duy nhất trong một Organization), nên tra trong danh sách Issue đã tải.
 */
export function resolveIssueRoute(param: string, workItems: readonly { id: string; issueKey: string }[]): IssueRouteTarget {
  const value = param.trim();
  if (GUID.test(value)) return { kind: 'id', id: value };
  if (!ISSUE_KEY.test(value)) return { kind: 'invalid' };
  const key = value.toUpperCase();
  return { kind: 'key', key, id: workItems.find(item => item.issueKey.toUpperCase() === key)?.id ?? null };
}
