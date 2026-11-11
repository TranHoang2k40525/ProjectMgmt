import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ConfirmDialogService } from '../../../../core/services/confirm-dialog.service';
import { ToastService } from '../../../../core/services/toast.service';
import { WorkspaceDataSource } from '../../data/workspace-data-source';
import { OrgMember } from '../../data/workspace.models';
import { WorkspaceContextService } from '../../workspace-context.service';
import { parseInviteEmails } from './invite-emails';
import { OrgMembersSettingsComponent } from './org-members-settings';

const member: OrgMember = { userId: 'ha', displayName: 'Lê Thu Hà', email: 'ha@example.com', avatarUrl: null, role: 'Member', joinedAt: '' };

describe('parseInviteEmails', () => {
  it('splits, lowercases, de-duplicates and separates invalid entries', () => {
    expect(parseInviteEmails('A@x.vn, a@x.vn;\nb@x.vn  sai-email')).toEqual({ valid: ['a@x.vn', 'b@x.vn'], invalid: ['sai-email'] });
  });
});

describe('OrgMembersSettingsComponent', () => {
  const data = {
    listMembers: vi.fn(() => of([member])),
    listRoles: vi.fn(() => of([
      { code: 'Owner', name: 'Chủ tổ chức', description: '', assignable: false },
      { code: 'Admin', name: 'Quản trị', description: '', assignable: true },
      { code: 'Member', name: 'Thành viên', description: '', assignable: true }
    ])),
    listInvitations: vi.fn(() => of([])),
    getInviteLink: vi.fn(() => of({ token: 'tok123', orgId: 'org', role: 'Member', createdAt: '' })),
    updateMemberRole: vi.fn(),
    inviteByEmail: vi.fn(),
    regenerateInviteLink: vi.fn()
  };
  const toast = { success: vi.fn(), error: vi.fn(), info: vi.fn() };

  beforeEach(() => {
    Object.values(data).forEach(fn => fn.mockClear());
    TestBed.configureTestingModule({
      providers: [
        { provide: WorkspaceDataSource, useValue: data },
        { provide: ToastService, useValue: toast },
        { provide: ConfirmDialogService, useValue: { confirm: (options: { onConfirm: () => void }) => options.onConfirm() } },
        { provide: WorkspaceContextService, useValue: { currentOrg: signal({ id: 'org', ownerId: 'me' }), reloadCurrent: vi.fn() } }
      ]
    });
  });

  function create(): OrgMembersSettingsComponent {
    const page = TestBed.runInInjectionContext(() => new OrgMembersSettingsComponent());
    page.ngOnInit();
    return page;
  }

  it('builds the invite URL from the current origin and hides the owner role', () => {
    const page = create();
    expect(page.inviteUrl()).toBe(`${location.origin}/invite/tok123`);
    expect(page.assignableRoles().map(role => role.code)).toEqual(['Admin', 'Member']);
  });

  it('puts the select back to the saved role when the change fails', () => {
    data.updateMemberRole.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { title: 'Không được' } })));
    const page = create();
    const select = { value: 'Admin' } as HTMLSelectElement;
    page.changeRole(member, select);
    expect(select.value).toBe('Member');
    expect(page.members()[0].role).toBe('Member');
    expect(toast.error).toHaveBeenCalled();
  });

  it('rejects invalid emails locally and keeps only addresses without an account after sending', () => {
    const page = create();
    page.emailText.set('sai-email');
    page.sendInvites();
    expect(data.inviteByEmail).not.toHaveBeenCalled();
    expect(page.inviteError()).toContain('sai-email');

    data.inviteByEmail.mockReturnValue(of([
      { email: 'a@x.vn', outcome: 'Sent' }, { email: 'b@x.vn', outcome: 'NoAccount' }, { email: 'c@x.vn', outcome: 'AlreadyMember' }
    ]));
    page.emailText.set('a@x.vn, b@x.vn, c@x.vn');
    page.sendInvites();
    expect(data.inviteByEmail).toHaveBeenCalledWith('org', ['a@x.vn', 'b@x.vn', 'c@x.vn'], 'Member');
    expect(page.emailText()).toBe('b@x.vn');
    expect(data.listInvitations).toHaveBeenCalledTimes(2);
  });

  it('regenerates the link with the chosen role after confirmation', () => {
    data.regenerateInviteLink.mockReturnValue(of({ token: 'new456', orgId: 'org', role: 'Admin', createdAt: '' }));
    const page = create();
    page.linkRole.set('Admin');
    page.createLink();
    expect(data.regenerateInviteLink).toHaveBeenCalledWith('org', 'Admin');
    expect(page.inviteUrl()).toContain('/invite/new456');
  });
});
