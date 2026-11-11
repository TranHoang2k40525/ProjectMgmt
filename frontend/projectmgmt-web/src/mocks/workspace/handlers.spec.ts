import { getResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { MockContext, WorkspaceMockBackend } from './mock-backend';
import { createWorkspaceHandlers } from './handlers';

const owner = { id: 'u-owner', email: 'owner@example.com', displayName: 'Chủ', avatarUrl: null };
const bob = { id: 'u-bob', email: 'bob@example.com', displayName: 'Bob', avatarUrl: null };
const contexts: Record<string, MockContext> = {
  'Bearer owner': { me: owner, visibleOrgs: [{ id: 'org-lab', name: 'HUCE Lab', ownerId: owner.id }], users: [owner, bob] },
  'Bearer bob': { me: bob, visibleOrgs: [], users: [owner, bob] }
};

const API = `${environment.apiBaseUrl}/v1`;

let stored: string | null = null;
/** Dự án thật mỗi người thấy qua GET /projects (theo token). */
let projectsByToken: Record<string, { orgId: string }[]> = {};
let handlers = createHandlers();

/** Backend thật được thay bằng ngữ cảnh cố định theo token. */
function createHandlers() {
  return createWorkspaceHandlers({
    backend: new WorkspaceMockBackend({ load: () => stored, save: value => (stored = value) }),
    context: request => Promise.resolve(contexts[request.headers.get('Authorization') ?? '']),
    memberIds: () => Promise.resolve(null),
    get: <T>(request: Request, path: string) => path === '/projects'
      ? Promise.resolve((projectsByToken[request.headers.get('Authorization') ?? ''] ?? []) as T)
      : Promise.reject(new Error('không gọi backend thật trong test'))
  });
}

async function call(token: string, method: string, path: string, body?: unknown) {
  const request = new Request(`${API}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  const response = await getResponse(handlers, request);
  if (!response) return { status: 0, json: null };
  const text = await response.text();
  return { status: response.status, json: text ? JSON.parse(text) : null };
}

describe('workspace MSW handlers', () => {
  beforeEach(() => {
    stored = null;
    projectsByToken = {};
    handlers = createHandlers();
  });

  it('counts a member of a project in the organization as an organization member', async () => {
    await call('owner', 'GET', '/users/me/organization-memberships');
    expect((await call('bob', 'GET', '/users/me/organization-memberships')).json).toEqual([]);

    projectsByToken['Bearer bob'] = [{ orgId: 'org-lab' }];
    const memberships = await call('bob', 'GET', '/users/me/organization-memberships');
    expect(memberships.json).toMatchObject([{ orgId: 'org-lab', orgName: 'HUCE Lab', role: 'Member' }]);
  }, 10_000);

  it('serves the contract endpoints with ProblemDetails errors', async () => {
    const members = await call('owner', 'GET', '/organizations/org-lab/members');
    expect(members.status).toBe(200);
    expect(members.json.map((member: { userId: string }) => member.userId)).toContain(owner.id);

    const forbidden = await call('bob', 'GET', '/organizations/org-lab/invitations');
    expect(forbidden.status).toBe(404);
    expect(forbidden.json).toMatchObject({ code: 'ORG_NOT_FOUND' });
  }, 10_000);

  it('runs the invitation flow across two accounts over HTTP', async () => {
    const sent = await call('owner', 'POST', '/organizations/org-lab/invitations', { emails: ['bob@example.com'], role: 'Member' });
    expect(sent.json).toEqual([{ email: 'bob@example.com', outcome: 'Sent' }]);

    const mine = await call('bob', 'GET', '/users/me/organization-invitations');
    expect(mine.json).toHaveLength(1);
    const accepted = await call('bob', 'POST', `/users/me/organization-invitations/${mine.json[0].id}/respond`, { accept: true });
    expect(accepted.json).toMatchObject({ orgId: 'org-lab', role: 'Member' });

    const memberships = await call('bob', 'GET', '/users/me/organization-memberships');
    expect(memberships.json.map((membership: { orgId: string }) => membership.orgId)).toEqual(['org-lab']);
  }, 10_000);

  it('returns 204 when the invite link is off and leaves existing backend endpoints unhandled', async () => {
    expect((await call('owner', 'GET', '/organizations/org-lab/invite-link')).status).toBe(204);
    expect((await call('owner', 'GET', '/organizations')).status).toBe(0);
    expect((await call('owner', 'GET', '/users/me')).status).toBe(0);
  }, 10_000);
});
