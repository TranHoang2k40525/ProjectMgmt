import { HttpResponse, delay, http } from 'msw';
import { environment } from '../../environments/environment';
import { RecentKind } from '../../app/features/workspace/data/workspace.models';
import { MockApiError, MockContext, WorkspaceMockBackend } from './mock-backend';
import { mockContext, projectMemberIds, realGet } from './real-api';

/** Phụ thuộc của handler: backend giả và các hàm đọc backend thật (test thay bằng stub). */
export interface WorkspaceHandlerDeps {
  backend: WorkspaceMockBackend;
  context: (request: Request) => Promise<MockContext>;
  memberIds: (request: Request, orgId: string) => Promise<string[] | null>;
  get: <T>(request: Request, path: string) => Promise<T>;
}

/**
 * Handler MSW cho các endpoint trong "Workspace API contract" mà backend CHƯA có. Endpoint đã có
 * (tổ chức, dự án, issue, người dùng…) không bị chặn — request đi thẳng tới backend thật.
 * Khi backend làm xong một endpoint, xóa handler tương ứng ở đây.
 */
const API = `${environment.apiBaseUrl}/v1`;
const STORAGE_KEY = 'pm-msw-workspace-db-v1';

export function browserStorage(key = STORAGE_KEY) {
  return {
    load: () => (typeof localStorage === 'undefined' ? null : localStorage.getItem(key)),
    save: (value: string) => {
      if (typeof localStorage !== 'undefined') localStorage.setItem(key, value);
    }
  };
}

interface HandlerArgs {
  request: Request;
  params: Record<string, string | readonly string[] | undefined>;
  ctx: MockContext;
}

export function createWorkspaceHandlers(deps: WorkspaceHandlerDeps) {
  function param(args: HandlerArgs, name: string): string {
    const value = args.params[name];
    return typeof value === 'string' ? value : '';
  }

  /** Lấy ngữ cảnh thật, đồng bộ vào backend giả, gọi xử lý; lỗi nghiệp vụ trả ProblemDetails kèm `code`. */
  function route(work: (args: HandlerArgs) => unknown | Promise<unknown>, successStatus = 200) {
    return async ({ request, params }: { request: Request; params: HandlerArgs['params'] }) => {
      await delay(120);
      try {
        const ctx = await deps.context(request);
        deps.backend.sync(ctx);
        const body = await work({ request, params, ctx });
        return body === undefined || body === null ? new HttpResponse(null, { status: 204 }) : HttpResponse.json(body, { status: successStatus });
      } catch (error) {
        if (error instanceof MockApiError) {
          return HttpResponse.json({ status: error.status, title: error.title, detail: error.title, code: error.code }, { status: error.status });
        }
        throw error;
      }
    };
  }

  async function body<T>(request: Request): Promise<Partial<T>> {
    try {
      return (await request.json()) as Partial<T>;
    } catch {
      return {};
    }
  }

  async function enrichMembers(args: HandlerArgs, orgId: string): Promise<void> {
    const ids = await deps.memberIds(args.request, orgId);
    if (ids) deps.backend.addKnownMembers(orgId, ids);
  }

  /** Tra tiêu đề mục "Gần đây" từ backend thật lúc ghi. */
  async function describeVisit(args: HandlerArgs, kind: RecentKind, id: string) {
    if (kind === 'organization') {
      const name = deps.backend.orgName(id) ?? args.ctx.visibleOrgs.find(org => org.id === id)?.name;
      if (!name) throw new MockApiError(404, 'ORG_NOT_FOUND', 'Không tìm thấy tổ chức.');
      return { kind, id, title: name, subtitle: 'Tổ chức', orgId: id };
    }
    const projectOf = (projectId: string) => deps.get<{ id: string; name: string; projectKey: string; orgId: string }>(args.request, `/projects/${projectId}`);
    if (kind === 'project') {
      const project = await projectOf(id);
      return { kind, id, title: project.name, subtitle: `${project.projectKey} · ${deps.backend.orgName(project.orgId) ?? ''}`, orgId: project.orgId };
    }
    const issue = await deps.get<{ title: string; issueKey: string; projectId: string }>(args.request, `/issues/${id}`);
    const project = await projectOf(issue.projectId);
    return { kind, id, title: issue.title, subtitle: `${issue.issueKey} · ${project.name}`, orgId: project.orgId };
  }

  return [
    // Tư cách thành viên của người gọi (gồm tổ chức được mời vào mà GET /organizations thật chưa trả về).
    // Ở trong một dự án của tổ chức cũng là thành viên tổ chức (wizard chỉ thêm người của tổ chức vào dự án).
    http.get(`${API}/users/me/organization-memberships`, route(async args => {
      await Promise.all(args.ctx.visibleOrgs.map(org => enrichMembers(args, org.id)));
      const myProjects = await deps.get<{ orgId: string }[]>(args.request, '/projects').catch(() => []);
      new Set(myProjects.map(project => project.orgId)).forEach(orgId => deps.backend.addKnownMembers(orgId, [args.ctx.me.id]));
      return deps.backend.myMemberships(args.ctx);
    })),

    http.get(`${API}/organizations/roles`, route(() => deps.backend.listRoles())),

    http.get(`${API}/organizations/:orgId/members`, route(async args => {
      await enrichMembers(args, param(args, 'orgId'));
      return deps.backend.listMembers(args.ctx, param(args, 'orgId'));
    })),
    http.put(`${API}/organizations/:orgId/members/:userId`, route(async args =>
      deps.backend.updateMemberRole(args.ctx, param(args, 'orgId'), param(args, 'userId'), (await body<{ role: string }>(args.request)).role))),
    http.delete(`${API}/organizations/:orgId/members/:userId`, route(args =>
      deps.backend.removeMember(args.ctx, param(args, 'orgId'), param(args, 'userId')))),

    http.post(`${API}/organizations/:orgId/invitations`, route(async args => {
      const request = await body<{ emails: string[]; role: string }>(args.request);
      return deps.backend.inviteByEmail(args.ctx, param(args, 'orgId'), request.emails, request.role);
    })),
    http.get(`${API}/organizations/:orgId/invitations`, route(args => deps.backend.listInvitations(args.ctx, param(args, 'orgId')))),
    http.delete(`${API}/organizations/:orgId/invitations/:invitationId`, route(args =>
      deps.backend.cancelInvitation(args.ctx, param(args, 'orgId'), param(args, 'invitationId')))),
    http.get(`${API}/users/me/organization-invitations`, route(args => deps.backend.listMyInvitations(args.ctx))),
    http.post(`${API}/users/me/organization-invitations/:invitationId/respond`, route(async args =>
      deps.backend.respondInvitation(args.ctx, param(args, 'invitationId'), (await body<{ accept: boolean }>(args.request)).accept))),

    http.get(`${API}/organizations/:orgId/invite-link`, route(args => deps.backend.getInviteLink(args.ctx, param(args, 'orgId')))),
    http.post(`${API}/organizations/:orgId/invite-link`, route(async args =>
      deps.backend.regenerateInviteLink(args.ctx, param(args, 'orgId'), (await body<{ role: string }>(args.request)).role))),
    http.delete(`${API}/organizations/:orgId/invite-link`, route(args => deps.backend.disableInviteLink(args.ctx, param(args, 'orgId')))),
    http.get(`${API}/invite-links/:token`, route(args => deps.backend.previewInvite(args.ctx, param(args, 'token')))),
    http.post(`${API}/invite-links/:token/accept`, route(args => deps.backend.acceptInvite(args.ctx, param(args, 'token')))),

    http.get(`${API}/organizations/:orgId/workflow-templates`, route(args => deps.backend.listTemplates(args.ctx, param(args, 'orgId')))),
    http.post(`${API}/organizations/:orgId/workflow-templates`, route(async args =>
      deps.backend.createTemplate(args.ctx, param(args, 'orgId'), await body(args.request)), 201)),
    http.put(`${API}/organizations/:orgId/workflow-templates/:templateId/default`, route(args =>
      deps.backend.setDefaultTemplate(args.ctx, param(args, 'orgId'), param(args, 'templateId')))),
    http.put(`${API}/organizations/:orgId/workflow-templates/:templateId`, route(async args =>
      deps.backend.updateTemplate(args.ctx, param(args, 'orgId'), param(args, 'templateId'), await body(args.request)))),
    http.delete(`${API}/organizations/:orgId/workflow-templates/:templateId`, route(args =>
      deps.backend.deleteTemplate(args.ctx, param(args, 'orgId'), param(args, 'templateId')))),

    http.get(`${API}/users/me/recent`, route(args => deps.backend.listRecent(args.ctx, new URL(args.request.url).searchParams.get('orgId')))),
    http.post(`${API}/users/me/recent`, route(async args => {
      const request = await body<{ kind: RecentKind; id: string }>(args.request);
      if (!request.kind || !request.id || !['organization', 'project', 'issue'].includes(request.kind)) {
        throw new MockApiError(400, 'RECENT_INVALID', 'Mục gần đây không hợp lệ.');
      }
      deps.backend.recordVisit(args.ctx, await describeVisit(args, request.kind, request.id));
      return undefined;
    }))
  ];
}

/** Handler dùng trong trình duyệt: lưu localStorage, đọc backend thật kèm token của người dùng. */
export const workspaceHandlers = createWorkspaceHandlers({
  backend: new WorkspaceMockBackend(browserStorage()),
  context: mockContext,
  memberIds: projectMemberIds,
  get: realGet
});
