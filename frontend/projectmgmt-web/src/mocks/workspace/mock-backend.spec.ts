import { beforeEach, describe, expect, it } from 'vitest';
import { MockApiError, MockContext, WorkspaceMockBackend } from './mock-backend';

const owner = { id: 'u-owner', email: 'owner@example.com', displayName: 'Chủ', avatarUrl: null };
const bob = { id: 'u-bob', email: 'bob@example.com', displayName: 'Bob', avatarUrl: null };
const lab = { id: 'org-lab', name: 'HUCE Lab', ownerId: owner.id };

function ctxOf(me: typeof owner, visibleOrgs = [lab]): MockContext {
  return { me, visibleOrgs, users: [owner, bob] };
}

function code(work: () => unknown): string {
  try {
    work();
  } catch (error) {
    return error instanceof MockApiError ? error.code : String(error);
  }
  return '';
}

describe('WorkspaceMockBackend', () => {
  let stored: string | null;
  let backend: WorkspaceMockBackend;
  const storage = { load: () => stored, save: (value: string) => (stored = value) };

  beforeEach(() => {
    stored = null;
    backend = new WorkspaceMockBackend(storage);
    backend.sync(ctxOf(owner));
  });

  it('makes the real owner an Owner member and keeps the data across instances', () => {
    expect(backend.listMembers(ctxOf(owner), lab.id).map(member => [member.userId, member.role])).toEqual([[owner.id, 'Owner']]);
    backend.addKnownMembers(lab.id, [bob.id]);
    const reloaded = new WorkspaceMockBackend(storage);
    expect(reloaded.listMembers(ctxOf(owner), lab.id).map(member => member.userId)).toEqual([owner.id, bob.id]);
  });

  it('lets another account accept an invitation and then see the organization', () => {
    const results = backend.inviteByEmail(ctxOf(owner), lab.id, ['BOB@example.com', 'nobody@example.com'], 'Admin');
    expect(results.map(result => result.outcome)).toEqual(['Sent', 'NoAccount']);

    const bobCtx = ctxOf(bob, []);
    backend.sync(bobCtx);
    const [invite] = backend.listMyInvitations(bobCtx);
    expect(invite).toMatchObject({ orgName: 'HUCE Lab', role: 'Admin', invitedByName: 'Chủ' });
    expect(backend.respondInvitation(bobCtx, invite.id, true)).toMatchObject({ orgId: lab.id, role: 'Admin', memberCount: 2 });
    expect(backend.myMemberships(bobCtx).map(membership => membership.orgId)).toEqual([lab.id]);
  });

  it('allows settings changes only for the owner and hides unknown organizations', () => {
    backend.addKnownMembers(lab.id, [bob.id]);
    const bobCtx = ctxOf(bob, []);
    expect(code(() => backend.inviteByEmail(bobCtx, lab.id, ['x@example.com'], 'Member'))).toBe('ORG_FORBIDDEN');
    expect(code(() => backend.listMembers(bobCtx, 'org-unknown'))).toBe('ORG_NOT_FOUND');
    expect(code(() => backend.updateMemberRole(ctxOf(owner), lab.id, owner.id, 'Member'))).toBe('ORG_OWNER_ROLE_LOCKED');
    expect(backend.updateMemberRole(ctxOf(owner), lab.id, bob.id, 'Admin').role).toBe('Admin');
  });

  it('revokes the old invite link and joins with the link role', () => {
    const first = backend.regenerateInviteLink(ctxOf(owner), lab.id, 'Member');
    const second = backend.regenerateInviteLink(ctxOf(owner), lab.id, 'Admin');
    const bobCtx = ctxOf(bob, []);
    backend.sync(bobCtx);
    expect(code(() => backend.previewInvite(bobCtx, first.token))).toBe('INVITE_LINK_INVALID');
    expect(backend.previewInvite(bobCtx, second.token)).toMatchObject({ orgName: 'HUCE Lab', alreadyMember: false });
    expect(backend.acceptInvite(bobCtx, second.token).role).toBe('Admin');
  });

  it('validates templates and protects presets and the default', () => {
    expect(code(() => backend.createTemplate(ctxOf(owner), lab.id, { name: 'Thiếu Done', managementType: 'Scrum', statuses: [{ name: 'A', category: 'ToDo' }, { name: 'B', category: 'InProgress' }] })))
      .toBe('WORKFLOW_TEMPLATE_INVALID');
    const created = backend.createTemplate(ctxOf(owner), lab.id, {
      name: 'Kanban hỗ trợ', managementType: 'Kanban', statuses: [{ name: 'Mới', category: 'ToDo' }, { name: 'Xong', category: 'Done' }]
    });
    expect(backend.setDefaultTemplate(ctxOf(owner), lab.id, created.id).isDefault).toBe(true);
    expect(code(() => backend.deleteTemplate(ctxOf(owner), lab.id, created.id))).toBe('WORKFLOW_TEMPLATE_IN_USE_AS_DEFAULT');
    expect(code(() => backend.updateTemplate(ctxOf(owner), lab.id, 'preset-scrum-std', {}))).toBe('WORKFLOW_TEMPLATE_PRESET_READONLY');
    expect(backend.listTemplates(ctxOf(owner), lab.id).filter(template => template.isDefault).map(template => template.id)).toEqual([created.id]);
  });

  it('keeps recent items per user and per organization', () => {
    backend.recordVisit(ctxOf(owner), { kind: 'project', id: 'p1', title: 'Dự án', subtitle: 'P1 · HUCE Lab', orgId: lab.id });
    backend.recordVisit(ctxOf(owner), { kind: 'organization', id: lab.id, title: 'HUCE Lab', subtitle: 'Tổ chức', orgId: lab.id });
    expect(backend.listRecent(ctxOf(owner), null).map(item => item.id)).toEqual([lab.id, 'p1']);
    expect(backend.listRecent(ctxOf(owner), 'org-other')).toEqual([]);
    expect(backend.listRecent(ctxOf(bob, []), null)).toEqual([]);
  });
});
