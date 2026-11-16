import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ProjectMembersPageComponent } from './project-members-page';
import { ProjectManagementService } from '../../core/services/project-management.service';
import { IdentityService } from '../../core/services/identity.service';
import { RbacApi } from '../../core/api/rbac.api';

describe('Project member invitation', () => {
  let response: Subject<boolean>;
  const invite = vi.fn(() => response);
  beforeEach(() => {
    response = new Subject(); invite.mockClear();
    TestBed.configureTestingModule({ providers: [
      { provide: ProjectManagementService, useValue: { currentProject: signal({ id: 'project', members: [] }), addProjectMember: invite } },
      { provide: IdentityService, useValue: { currentUser: signal(null), authState: signal({ currentUser: null }), hasPermission: () => true } },
      { provide: RbacApi, useValue: {} }
    ] });
  });
  function form(): ProjectMembersPageComponent {
    const page = TestBed.runInInjectionContext(() => new ProjectMembersPageComponent());
    page.isInviteModalOpen = true; page.newMemberName = 'Member'; page.newMemberEmail = 'member@huce.edu.vn';
    return page;
  }
  it('keeps the dialog open until saved and blocks duplicate submissions', () => {
    const page = form(); page.submitInvite(); page.submitInvite();
    expect(invite).toHaveBeenCalledTimes(1);
    expect(page.isInviteModalOpen).toBe(true);
    response.next(true);
    expect(page.isInviteModalOpen).toBe(false);
    expect(page.newMemberEmail).toBe('');
  });
  it('keeps form contents and displays the API failure', () => {
    const page = form(); page.submitInvite(); response.error({ detail: 'Email not found' });
    expect(page.isInviteModalOpen).toBe(true);
    expect(page.newMemberEmail).toBe('member@huce.edu.vn');
    expect(page.saving).toBe(false);
    expect(page.error).toBe('Email not found');
  });
});
