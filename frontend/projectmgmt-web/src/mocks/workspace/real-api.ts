import { bypass } from 'msw';
import { environment } from '../../environments/environment';
import { MockApiError, MockContext, MockUser, RealOrg } from './mock-backend';

/** Gọi backend thật từ trong handler MSW, kèm token của người dùng, không bị MSW chặn lại. */
export async function realGet<T>(request: Request, path: string): Promise<T> {
  const headers = new Headers({ Accept: 'application/json' });
  const auth = request.headers.get('Authorization');
  if (auth) headers.set('Authorization', auth);
  const response = await fetch(bypass(new Request(`${environment.apiBaseUrl}${path}`, { headers })));
  if (response.status === 401) throw new MockApiError(401, 'AUTH_UNAUTHENTICATED', 'Phiên đăng nhập đã hết hạn.');
  if (!response.ok) throw new MockApiError(response.status, 'UPSTREAM_ERROR', `Backend trả lỗi ${response.status} khi đọc ${path}.`);
  return response.json() as Promise<T>;
}

interface CachedContext {
  value: Promise<MockContext>;
  at: number;
}

const CONTEXT_TTL_MS = 3000;
const contextCache = new Map<string, CachedContext>();

/**
 * Người gọi, tổ chức họ thấy và danh bạ người dùng — lấy từ backend thật, nhớ vài giây
 * để một lần tải trang (nhiều request song song) chỉ gọi backend một lượt.
 */
export function mockContext(request: Request): Promise<MockContext> {
  const auth = request.headers.get('Authorization');
  if (!auth) return Promise.reject(new MockApiError(401, 'AUTH_UNAUTHENTICATED', 'Phiên đăng nhập đã hết hạn.'));
  const cached = contextCache.get(auth);
  if (cached && Date.now() - cached.at < CONTEXT_TTL_MS) return cached.value;

  const value = Promise.all([
    realGet<{ userId?: string; email?: string; fullName?: string; avatarUrl?: string | null }>(request, '/users/me'),
    realGet<{ id: string; name: string; ownerId: string }[]>(request, '/organizations'),
    realGet<{ userId: string; displayName: string; email?: string; avatarUrl?: string | null }[]>(request, '/users')
  ]).then(([me, orgs, users]) => {
    if (!me.userId) throw new MockApiError(401, 'AUTH_UNAUTHENTICATED', 'Phiên đăng nhập đã hết hạn.');
    const self: MockUser = { id: me.userId, email: me.email ?? '', displayName: me.fullName || me.email || 'Người dùng', avatarUrl: me.avatarUrl ?? null };
    return {
      me: self,
      visibleOrgs: orgs.map((org): RealOrg => ({ id: org.id, name: org.name, ownerId: org.ownerId })),
      users: users.map(user => ({ id: user.userId, displayName: user.displayName, email: user.email ?? '', avatarUrl: user.avatarUrl ?? null }))
    };
  });
  value.catch(() => contextCache.delete(auth));
  contextCache.set(auth, { value, at: Date.now() });
  return value;
}

const enrichedAt = new Map<string, number>();
const ENRICH_TTL_MS = 60_000;

/** userId của mọi người trong các dự án (thật) thuộc tổ chức — backend chưa có bảng thành viên tổ chức. */
export async function projectMemberIds(request: Request, orgId: string): Promise<string[] | null> {
  const last = enrichedAt.get(orgId);
  if (last && Date.now() - last < ENRICH_TTL_MS) return null;
  const projects = await realGet<{ id: string; orgId: string }[]>(request, '/projects');
  const ids = await Promise.all(projects.filter(project => project.orgId === orgId).map(project =>
    realGet<{ members?: { userId?: string }[] }>(request, `/projects/${project.id}/members`)
      .then(result => (result.members ?? []).map(member => member.userId ?? ''))
      .catch(() => [] as string[])));
  enrichedAt.set(orgId, Date.now());
  return ids.flat().filter(Boolean);
}

export function resetRealApiCaches(): void {
  contextCache.clear();
  enrichedAt.clear();
}
