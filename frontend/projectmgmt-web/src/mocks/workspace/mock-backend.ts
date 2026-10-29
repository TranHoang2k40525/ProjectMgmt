import {
  InviteEmailResult,
  InviteLink,
  InviteLinkPreview,
  ManagementType,
  MyInvitation,
  OrgInvitation,
  OrgMember,
  OrgMembership,
  OrgRoleCode,
  OrgRoleOption,
  RecentItem,
  RecentKind,
  SaveWorkflowTemplateRequest,
  WorkflowStatusDef,
  WorkflowTemplate
} from '../../app/features/workspace/data/workspace.models';

/**
 * "Backend giả" cho các endpoint BE chưa có (thành viên & vai trò tổ chức, lời mời, link mời, mẫu workflow của tổ chức,
 * Gần đây). Chạy trong handler MSW: dữ liệu nằm trong một khối JSON lưu localStorage, dùng chung cho mọi tài khoản trên
 * cùng trình duyệt — giống một server thật (A mời B, đăng nhập B là thấy lời mời).
 * Dữ liệu thật (tổ chức người gọi thấy được, danh bạ người dùng, thành viên dự án) được handler lấy từ backend thật
 * rồi truyền vào qua {@link MockContext}. Ràng buộc và mã lỗi theo tài liệu "Workspace API contract".
 */

export class MockApiError extends Error {
  constructor(readonly status: number, readonly code: string, readonly title: string) {
    super(title);
  }
}

export interface MockUser {
  id: string;
  displayName: string;
  email: string;
  avatarUrl: string | null;
}

export interface RealOrg {
  id: string;
  name: string;
  ownerId: string;
}

/** Thông tin thật của lời gọi hiện tại, handler lấy từ backend thật (kèm token người dùng). */
export interface MockContext {
  me: MockUser;
  /** Tổ chức người gọi thấy qua GET /organizations thật (là chủ hoặc có quyền). */
  visibleOrgs: RealOrg[];
  users: MockUser[];
}

interface Db {
  version: 1;
  orgs: RealOrg[];
  users: MockUser[];
  members: { orgId: string; userId: string; role: OrgRoleCode; joinedAt: string }[];
  invitations: { id: string; orgId: string; email: string; role: OrgRoleCode; invitedBy: string; createdAt: string; status: 'Pending' | 'Accepted' | 'Declined' | 'Cancelled' }[];
  inviteLinks: { token: string; orgId: string; role: OrgRoleCode; createdBy: string; createdAt: string; isActive: boolean }[];
  templates: { id: string; orgId: string; name: string; managementType: ManagementType; statuses: WorkflowStatusDef[] }[];
  defaultTemplate: Record<string, string>;
  recent: (RecentItem & { userId: string })[];
}

export interface Storage {
  load(): string | null;
  save(value: string): void;
}

const todo = (name: string): WorkflowStatusDef => ({ name, category: 'ToDo' });
const doing = (name: string): WorkflowStatusDef => ({ name, category: 'InProgress' });
const done = (name: string): WorkflowStatusDef => ({ name, category: 'Done' });

/** Mẫu có sẵn — cùng tên trạng thái với `ProjectCustomizationController.ApplyTemplate` của backend. */
export const PRESET_TEMPLATES: readonly { id: string; name: string; managementType: ManagementType; statuses: WorkflowStatusDef[] }[] = [
  { id: 'preset-scrum-std', name: 'Scrum chuẩn', managementType: 'Scrum', statuses: [todo('To Do'), doing('In Progress'), doing('Code Review'), done('Done')] },
  { id: 'preset-agile-qa', name: 'Agile + QA', managementType: 'Scrum', statuses: [todo('To Do'), doing('In Progress'), doing('Code Review'), doing('QA Testing'), done('Done')] },
  { id: 'preset-kanban-simple', name: 'Kanban đơn giản', managementType: 'Kanban', statuses: [todo('To Do'), doing('In Progress'), done('Done')] },
  { id: 'preset-enterprise', name: 'Enterprise', managementType: 'Scrum', statuses: [todo('Backlog'), doing('Spec Review'), doing('In Progress'), doing('Code Review'), doing('QA Verification'), done('Done')] }
];

const ROLE_OPTIONS: OrgRoleOption[] = [
  { code: 'Owner', name: 'Chủ tổ chức', description: 'Người tạo tổ chức, toàn quyền cài đặt.', assignable: false },
  { code: 'Admin', name: 'Quản trị', description: 'Quản lý dự án và hỗ trợ thành viên.', assignable: true },
  { code: 'Member', name: 'Thành viên', description: 'Tham gia các dự án được thêm vào.', assignable: true }
];

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const MAX_INVITE_EMAILS = 20;
const MAX_RECENT_PER_USER = 30;

function fail(status: number, code: string, title: string): never {
  throw new MockApiError(status, code, title);
}

function newId(prefix: string): string {
  const random = typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now().toString(36)}${Math.random().toString(36).slice(2)}`;
  return `${prefix}-${random}`;
}

function newToken(): string {
  const bytes = new Uint8Array(18);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
}

function emptyDb(): Db {
  return { version: 1, orgs: [], users: [], members: [], invitations: [], inviteLinks: [], templates: [], defaultTemplate: {}, recent: [] };
}

export class WorkspaceMockBackend {
  private db: Db;

  constructor(private readonly storage: Storage, private readonly now: () => Date = () => new Date()) {
    this.db = this.read();
  }

  /** Đưa dữ liệu thật vào trước mỗi lời gọi: tổ chức, danh bạ, và tư cách thành viên của người gọi. */
  sync(ctx: MockContext): void {
    const db = this.db;
    for (const user of [ctx.me, ...ctx.users]) {
      const existing = db.users.find(item => item.id === user.id);
      if (existing) Object.assign(existing, user); else db.users.push({ ...user });
    }
    for (const org of ctx.visibleOrgs) {
      const existing = db.orgs.find(item => item.id === org.id);
      if (existing) Object.assign(existing, org); else db.orgs.push({ ...org });
      this.ensureMember(org.id, org.ownerId, 'Owner');
      this.ensureMember(org.id, ctx.me.id, org.ownerId === ctx.me.id ? 'Owner' : 'Member');
    }
    this.persist();
  }

  /** Người trong các dự án của tổ chức được coi là thành viên tổ chức (backend chưa có bảng thành viên tổ chức). */
  addKnownMembers(orgId: string, userIds: string[]): void {
    if (!this.db.orgs.some(org => org.id === orgId)) return;
    userIds.filter(Boolean).forEach(userId => this.ensureMember(orgId, userId, 'Member'));
    this.persist();
  }

  // --- Tư cách thành viên của người gọi ---------------------------------------

  myMemberships(ctx: MockContext): OrgMembership[] {
    return this.db.members
      .filter(member => member.userId === ctx.me.id)
      .map(member => ({ member, org: this.db.orgs.find(org => org.id === member.orgId) }))
      .filter((entry): entry is { member: Db['members'][number]; org: RealOrg } => !!entry.org)
      .map(({ member, org }) => ({
        orgId: org.id,
        orgName: org.name,
        ownerId: org.ownerId,
        role: member.role,
        memberCount: this.db.members.filter(item => item.orgId === org.id).length,
        joinedAt: member.joinedAt
      }));
  }

  // --- Thành viên --------------------------------------------------------------

  listRoles(): OrgRoleOption[] {
    return ROLE_OPTIONS.map(role => ({ ...role }));
  }

  listMembers(ctx: MockContext, orgId: string): OrgMember[] {
    this.requireMember(ctx, orgId);
    const rank: Record<OrgRoleCode, number> = { Owner: 0, Admin: 1, Member: 2 };
    return this.db.members
      .filter(member => member.orgId === orgId)
      .map(member => this.toMember(member))
      .sort((a, b) => rank[a.role] - rank[b.role] || a.displayName.localeCompare(b.displayName, 'vi'));
  }

  updateMemberRole(ctx: MockContext, orgId: string, userId: string, role: unknown): OrgMember {
    const org = this.requireOwner(ctx, orgId);
    if (role !== 'Admin' && role !== 'Member') fail(400, 'ORG_ROLE_INVALID', 'Không thể gán vai trò này.');
    if (userId === org.ownerId) fail(409, 'ORG_OWNER_ROLE_LOCKED', 'Không thể đổi vai trò của chủ tổ chức.');
    const member = this.memberRow(orgId, userId) ?? fail(404, 'ORG_MEMBER_NOT_FOUND', 'Người này không còn trong tổ chức.');
    member.role = role;
    this.persist();
    return this.toMember(member);
  }

  removeMember(ctx: MockContext, orgId: string, userId: string): void {
    const org = this.requireOwner(ctx, orgId);
    if (userId === org.ownerId) fail(409, 'ORG_OWNER_ROLE_LOCKED', 'Không thể gỡ chủ tổ chức.');
    if (!this.memberRow(orgId, userId)) fail(404, 'ORG_MEMBER_NOT_FOUND', 'Người này không còn trong tổ chức.');
    this.db.members = this.db.members.filter(member => !(member.orgId === orgId && member.userId === userId));
    this.persist();
  }

  // --- Lời mời qua email -------------------------------------------------------

  inviteByEmail(ctx: MockContext, orgId: string, emails: unknown, role: unknown): InviteEmailResult[] {
    this.requireOwner(ctx, orgId);
    if (role !== 'Admin' && role !== 'Member') fail(400, 'ORG_ROLE_INVALID', 'Vai trò mời không hợp lệ.');
    const list = Array.isArray(emails) ? emails.filter((email): email is string => typeof email === 'string') : [];
    const unique = [...new Set(list.map(email => email.trim().toLowerCase()).filter(Boolean))];
    if (unique.length === 0) fail(400, 'INVITE_EMAILS_REQUIRED', 'Nhập ít nhất một email.');
    if (unique.length > MAX_INVITE_EMAILS) fail(400, 'INVITE_EMAILS_TOO_MANY', `Mỗi lần mời tối đa ${MAX_INVITE_EMAILS} email.`);
    const invalid = unique.find(email => !EMAIL_PATTERN.test(email));
    if (invalid) fail(400, 'INVITE_EMAIL_INVALID', `Email không hợp lệ: ${invalid}`);

    const results = unique.map((email): InviteEmailResult => {
      const user = this.db.users.find(candidate => candidate.email.toLowerCase() === email);
      if (!user) return { email, outcome: 'NoAccount' };
      if (this.memberRow(orgId, user.id)) return { email, outcome: 'AlreadyMember' };
      if (this.db.invitations.some(invite => invite.orgId === orgId && invite.email === email && invite.status === 'Pending')) {
        return { email, outcome: 'AlreadyInvited' };
      }
      this.db.invitations.push({ id: newId('invite'), orgId, email, role, invitedBy: ctx.me.id, createdAt: this.iso(), status: 'Pending' });
      return { email, outcome: 'Sent' };
    });
    this.persist();
    return results;
  }

  listInvitations(ctx: MockContext, orgId: string): OrgInvitation[] {
    this.requireOwner(ctx, orgId);
    return this.db.invitations
      .filter(invite => invite.orgId === orgId && invite.status === 'Pending')
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
      .map(invite => ({ id: invite.id, orgId, email: invite.email, role: invite.role, invitedByName: this.userName(invite.invitedBy), createdAt: invite.createdAt }));
  }

  cancelInvitation(ctx: MockContext, orgId: string, invitationId: string): void {
    this.requireOwner(ctx, orgId);
    const invite = this.db.invitations.find(item => item.id === invitationId && item.orgId === orgId && item.status === 'Pending')
      ?? fail(404, 'INVITATION_NOT_FOUND', 'Lời mời không còn hiệu lực.');
    invite.status = 'Cancelled';
    this.persist();
  }

  listMyInvitations(ctx: MockContext): MyInvitation[] {
    const email = ctx.me.email.toLowerCase();
    return this.db.invitations
      .filter(invite => invite.email === email && invite.status === 'Pending')
      .map(invite => ({
        id: invite.id,
        orgId: invite.orgId,
        orgName: this.db.orgs.find(org => org.id === invite.orgId)?.name ?? '',
        role: invite.role,
        invitedByName: this.userName(invite.invitedBy),
        createdAt: invite.createdAt
      }));
  }

  respondInvitation(ctx: MockContext, invitationId: string, accept: unknown): OrgMembership | null {
    const invite = this.db.invitations.find(item => item.id === invitationId && item.status === 'Pending' && item.email === ctx.me.email.toLowerCase())
      ?? fail(404, 'INVITATION_NOT_FOUND', 'Lời mời không còn hiệu lực.');
    invite.status = accept === true ? 'Accepted' : 'Declined';
    if (accept === true) this.ensureMember(invite.orgId, ctx.me.id, invite.role);
    this.persist();
    return accept === true ? this.membershipOf(ctx, invite.orgId) : null;
  }

  // --- Link mời ----------------------------------------------------------------

  getInviteLink(ctx: MockContext, orgId: string): InviteLink | null {
    this.requireOwner(ctx, orgId);
    const link = this.db.inviteLinks.find(item => item.orgId === orgId && item.isActive);
    return link ? { token: link.token, orgId, role: link.role, createdAt: link.createdAt } : null;
  }

  regenerateInviteLink(ctx: MockContext, orgId: string, role: unknown): InviteLink {
    this.requireOwner(ctx, orgId);
    if (role !== 'Admin' && role !== 'Member') fail(400, 'ORG_ROLE_INVALID', 'Vai trò của link mời không hợp lệ.');
    this.db.inviteLinks.filter(item => item.orgId === orgId).forEach(item => (item.isActive = false));
    const link: Db['inviteLinks'][number] = { token: newToken(), orgId, role, createdBy: ctx.me.id, createdAt: this.iso(), isActive: true };
    this.db.inviteLinks.push(link);
    this.persist();
    return { token: link.token, orgId, role, createdAt: link.createdAt };
  }

  disableInviteLink(ctx: MockContext, orgId: string): void {
    this.requireOwner(ctx, orgId);
    this.db.inviteLinks.filter(item => item.orgId === orgId).forEach(item => (item.isActive = false));
    this.persist();
  }

  previewInvite(ctx: MockContext, token: string): InviteLinkPreview {
    const link = this.activeLink(token);
    const org = this.orgRow(link.orgId);
    return { token, orgId: org.id, orgName: org.name, invitedByName: this.userName(link.createdBy), role: link.role, alreadyMember: !!this.memberRow(org.id, ctx.me.id) };
  }

  acceptInvite(ctx: MockContext, token: string): OrgMembership {
    const link = this.activeLink(token);
    this.ensureMember(link.orgId, ctx.me.id, link.role);
    this.persist();
    return this.membershipOf(ctx, link.orgId)!;
  }

  // --- Mẫu workflow ------------------------------------------------------------

  listTemplates(ctx: MockContext, orgId: string): WorkflowTemplate[] {
    this.requireMember(ctx, orgId);
    const defaultId = this.defaultTemplateId(orgId);
    return [
      ...PRESET_TEMPLATES.map(preset => ({ ...structuredClone(preset), orgId: null, isPreset: true, isDefault: preset.id === defaultId })),
      ...this.db.templates.filter(template => template.orgId === orgId)
        .map(template => ({ ...structuredClone(template), isPreset: false, isDefault: template.id === defaultId }))
    ];
  }

  createTemplate(ctx: MockContext, orgId: string, request: unknown): WorkflowTemplate {
    this.requireOwner(ctx, orgId);
    const row = { id: newId('template'), orgId, ...this.validateTemplate(orgId, request, null) };
    this.db.templates.push(row);
    this.persist();
    return { ...structuredClone(row), isPreset: false, isDefault: false };
  }

  updateTemplate(ctx: MockContext, orgId: string, templateId: string, request: unknown): WorkflowTemplate {
    this.requireOwner(ctx, orgId);
    const row = this.customTemplate(orgId, templateId);
    Object.assign(row, this.validateTemplate(orgId, request, templateId));
    this.persist();
    return { ...structuredClone(row), isPreset: false, isDefault: this.defaultTemplateId(orgId) === row.id };
  }

  deleteTemplate(ctx: MockContext, orgId: string, templateId: string): void {
    this.requireOwner(ctx, orgId);
    this.customTemplate(orgId, templateId);
    if (this.defaultTemplateId(orgId) === templateId) {
      fail(409, 'WORKFLOW_TEMPLATE_IN_USE_AS_DEFAULT', 'Đang là mẫu mặc định; hãy chọn mẫu mặc định khác trước khi xóa.');
    }
    this.db.templates = this.db.templates.filter(template => template.id !== templateId);
    this.persist();
  }

  setDefaultTemplate(ctx: MockContext, orgId: string, templateId: string): WorkflowTemplate {
    this.requireOwner(ctx, orgId);
    const template = this.listTemplates(ctx, orgId).find(item => item.id === templateId)
      ?? fail(404, 'WORKFLOW_TEMPLATE_NOT_FOUND', 'Không tìm thấy mẫu workflow.');
    this.db.defaultTemplate[orgId] = templateId;
    this.persist();
    return { ...template, isDefault: true };
  }

  // --- Gần đây -----------------------------------------------------------------

  listRecent(ctx: MockContext, orgId: string | null): RecentItem[] {
    const accessible = new Set(this.myMemberships(ctx).map(membership => membership.orgId));
    return this.db.recent
      .filter(item => item.userId === ctx.me.id && accessible.has(item.orgId) && (!orgId || item.orgId === orgId))
      .sort((a, b) => b.visitedAt.localeCompare(a.visitedAt))
      .slice(0, 8)
      .map(item => ({ kind: item.kind, id: item.id, title: item.title, subtitle: item.subtitle, orgId: item.orgId, visitedAt: item.visitedAt }));
  }

  /** Handler đã tra tiêu đề từ backend thật; lưu ảnh chụp để hiển thị nhanh. */
  recordVisit(ctx: MockContext, item: { kind: RecentKind; id: string; title: string; subtitle: string; orgId: string }): void {
    this.db.recent = [
      { ...item, userId: ctx.me.id, visitedAt: this.iso() },
      ...this.db.recent.filter(entry => !(entry.userId === ctx.me.id && entry.kind === item.kind && entry.id === item.id))
    ];
    const mine = this.db.recent.filter(entry => entry.userId === ctx.me.id);
    if (mine.length > MAX_RECENT_PER_USER) {
      const drop = new Set(mine.slice(MAX_RECENT_PER_USER));
      this.db.recent = this.db.recent.filter(entry => !drop.has(entry));
    }
    this.persist();
  }

  orgName(orgId: string): string | null {
    return this.db.orgs.find(org => org.id === orgId)?.name ?? null;
  }

  // --- Hạ tầng -----------------------------------------------------------------

  private read(): Db {
    try {
      const raw = this.storage.load();
      const parsed = raw ? JSON.parse(raw) as Db : null;
      return parsed?.version === 1 ? parsed : emptyDb();
    } catch {
      return emptyDb();
    }
  }

  private persist(): void {
    try {
      this.storage.save(JSON.stringify(this.db));
    } catch {
      /* localStorage bị chặn/đầy: dữ liệu vẫn dùng được tới khi tải lại trang. */
    }
  }

  private iso(): string {
    return this.now().toISOString();
  }

  private orgRow(orgId: string): RealOrg {
    return this.db.orgs.find(org => org.id === orgId) ?? fail(404, 'ORG_NOT_FOUND', 'Không tìm thấy tổ chức.');
  }

  private memberRow(orgId: string, userId: string) {
    return this.db.members.find(member => member.orgId === orgId && member.userId === userId);
  }

  private ensureMember(orgId: string, userId: string, role: OrgRoleCode): void {
    const existing = this.memberRow(orgId, userId);
    if (existing) {
      if (role === 'Owner') existing.role = 'Owner';
      return;
    }
    this.db.members.push({ orgId, userId, role, joinedAt: this.iso() });
  }

  /** Không phải thành viên thì trả 404 như tổ chức không tồn tại, tránh lộ thông tin. */
  private requireMember(ctx: MockContext, orgId: string): RealOrg {
    const org = this.orgRow(orgId);
    if (!this.memberRow(orgId, ctx.me.id)) fail(404, 'ORG_NOT_FOUND', 'Không tìm thấy tổ chức.');
    return org;
  }

  private requireOwner(ctx: MockContext, orgId: string): RealOrg {
    const org = this.requireMember(ctx, orgId);
    if (org.ownerId !== ctx.me.id) fail(403, 'ORG_FORBIDDEN', 'Chỉ chủ tổ chức được thay đổi cài đặt.');
    return org;
  }

  private membershipOf(ctx: MockContext, orgId: string): OrgMembership | null {
    return this.myMemberships(ctx).find(membership => membership.orgId === orgId) ?? null;
  }

  private activeLink(token: string) {
    return this.db.inviteLinks.find(link => link.token === token && link.isActive)
      ?? fail(404, 'INVITE_LINK_INVALID', 'Link mời không còn hiệu lực.');
  }

  private userName(userId: string): string {
    return this.db.users.find(user => user.id === userId)?.displayName ?? 'Người dùng';
  }

  private toMember(member: Db['members'][number]): OrgMember {
    const user = this.db.users.find(candidate => candidate.id === member.userId);
    return { userId: member.userId, displayName: user?.displayName ?? 'Người dùng', email: user?.email ?? '', avatarUrl: user?.avatarUrl ?? null, role: member.role, joinedAt: member.joinedAt };
  }

  private defaultTemplateId(orgId: string): string {
    return this.db.defaultTemplate[orgId] ?? PRESET_TEMPLATES[0].id;
  }

  private customTemplate(orgId: string, templateId: string) {
    if (PRESET_TEMPLATES.some(item => item.id === templateId)) {
      fail(409, 'WORKFLOW_TEMPLATE_PRESET_READONLY', 'Mẫu có sẵn không sửa hoặc xóa được; hãy tạo bản sao.');
    }
    return this.db.templates.find(item => item.id === templateId && item.orgId === orgId)
      ?? fail(404, 'WORKFLOW_TEMPLATE_NOT_FOUND', 'Không tìm thấy mẫu workflow.');
  }

  private validateTemplate(orgId: string, input: unknown, selfId: string | null): SaveWorkflowTemplateRequest {
    const request = (input ?? {}) as Partial<SaveWorkflowTemplateRequest>;
    const name = typeof request.name === 'string' ? request.name.trim() : '';
    if (name.length < 2 || name.length > 60) fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Tên mẫu từ 2 đến 60 ký tự.');
    if (request.managementType !== 'Scrum' && request.managementType !== 'Kanban') fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Loại hình quản lý không hợp lệ.');
    if (this.db.templates.some(item => item.orgId === orgId && item.id !== selfId && item.name.toLowerCase() === name.toLowerCase())) {
      fail(409, 'WORKFLOW_TEMPLATE_NAME_DUPLICATE', 'Tổ chức đã có mẫu cùng tên.');
    }
    const categories = ['ToDo', 'InProgress', 'Done'];
    const statuses = (Array.isArray(request.statuses) ? request.statuses : []).map(status => ({
      name: typeof status?.name === 'string' ? status.name.trim() : '',
      category: categories.includes(status?.category) ? status.category : 'InProgress'
    })) as WorkflowStatusDef[];
    if (statuses.length < 2 || statuses.length > 20) fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Mẫu cần từ 2 đến 20 trạng thái.');
    if (statuses.some(status => !status.name || status.name.length > 40)) fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Tên trạng thái không được trống và tối đa 40 ký tự.');
    if (new Set(statuses.map(status => status.name.toLowerCase())).size !== statuses.length) fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Tên trạng thái không được trùng nhau.');
    if (!statuses.some(status => status.category === 'ToDo') || !statuses.some(status => status.category === 'Done')) {
      fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Cần ít nhất một trạng thái "Cần làm" và một trạng thái "Hoàn thành".');
    }
    return { name, managementType: request.managementType, statuses };
  }
}
