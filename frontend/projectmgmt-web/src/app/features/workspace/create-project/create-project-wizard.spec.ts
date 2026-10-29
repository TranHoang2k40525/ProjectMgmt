import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { WorkspaceDataSource } from '../data/workspace-data-source';
import { OrgMember, WorkflowTemplate } from '../data/workspace.models';
import { WorkspaceContextService } from '../workspace-context.service';
import { CreateProjectWizardComponent } from './create-project-wizard';

const members: OrgMember[] = [
  { userId: 'me', displayName: 'Tôi', email: 'me@example.com', avatarUrl: null, role: 'Owner', joinedAt: '' },
  { userId: 'ha', displayName: 'Lê Thu Hà', email: 'ha@example.com', avatarUrl: null, role: 'Member', joinedAt: '' },
  { userId: 'khang', displayName: 'Phạm Đức Khang', email: 'khang@example.com', avatarUrl: null, role: 'Member', joinedAt: '' }
];
const templates: WorkflowTemplate[] = [
  { id: 'preset-scrum-std', orgId: null, name: 'Scrum chuẩn', managementType: 'Scrum', isPreset: true, isDefault: false,
    statuses: [{ name: 'To Do', category: 'ToDo' }, { name: 'Done', category: 'Done' }] },
  { id: 'org-kanban', orgId: 'org', name: 'Kanban nhóm', managementType: 'Kanban', isPreset: false, isDefault: true,
    statuses: [{ name: 'Mới', category: 'ToDo' }, { name: 'Xong', category: 'Done' }] }
];

describe('CreateProjectWizardComponent', () => {
  const data = {
    listMembers: vi.fn(() => of(members)),
    listWorkflowTemplates: vi.fn(() => of(templates)),
    isProjectKeyAvailable: vi.fn(() => of(true)),
    createProject: vi.fn()
  };

  beforeEach(() => {
    Object.values(data).forEach(fn => fn.mockClear());
    data.isProjectKeyAvailable.mockImplementation(() => of(true));
    TestBed.configureTestingModule({
      providers: [
        { provide: WorkspaceDataSource, useValue: data },
        { provide: WorkspaceContextService, useValue: { currentUserId: signal('me') } }
      ]
    });
  });

  function create(): CreateProjectWizardComponent {
    const fixture = TestBed.createComponent(CreateProjectWizardComponent);
    fixture.componentRef.setInput('orgId', 'org');
    fixture.componentInstance.ngOnInit();
    return fixture.componentInstance;
  }

  function fillStep1(wizard: CreateProjectWizardComponent): void {
    wizard.onNameChange('Hệ thống quản lý thư viện');
    wizard.next();
  }

  it('defaults the lead to the creator and the template to the organization default', () => {
    const wizard = create();
    expect(wizard.leadUserId()).toBe('me');
    expect(wizard.templateId()).toBe('org-kanban');
  });

  it('suggests the key from the name until the user types one', () => {
    const wizard = create();
    wizard.onNameChange('Hệ thống quản lý');
    expect(wizard.projectKey()).toBe('HTQL');
    wizard.onKeyChange('lib 1');
    wizard.onNameChange('Hệ thống quản lý thư viện');
    expect(wizard.projectKey()).toBe('LIB1');
  });

  it('stays on step 1 when the key is taken', () => {
    data.isProjectKeyAvailable.mockImplementation(() => of(false));
    const wizard = create();
    fillStep1(wizard);
    expect(wizard.step()).toBe(1);
    expect(wizard.keyError()).toContain('HTQLTV');
  });

  it('keeps entered data when going back and blocks duplicate issue type names', () => {
    const wizard = create();
    fillStep1(wizard);
    expect(wizard.step()).toBe(2);
    wizard.renameIssueType('Story', 'Task');
    wizard.next();
    expect(wizard.step()).toBe(2);
    expect(wizard.issueTypeErrors()[2]).toBe('Trùng với kiểu khác.');
    wizard.renameIssueType('Story', 'Use case');
    wizard.back();
    expect(wizard.name()).toBe('Hệ thống quản lý thư viện');
    wizard.next();
    wizard.next();
    expect(wizard.step()).toBe(3);
  });

  it('excludes creator and lead from candidates and sends the full request', () => {
    const wizard = create();
    wizard.onLeadChange('ha');
    fillStep1(wizard);
    wizard.renameIssueType('Story', 'Use case');
    wizard.next();
    expect(wizard.candidates().map(member => member.userId)).toEqual(['khang']);
    wizard.toggleMember('khang', true);
    wizard.setMemberRole('khang', 'ScrumMaster');
    data.createProject.mockReturnValue(of({ id: 'p1', projectKey: 'HTQLTV' }));
    wizard.submit();
    expect(data.createProject).toHaveBeenCalledWith('org', expect.objectContaining({
      name: 'Hệ thống quản lý thư viện',
      projectKey: 'HTQLTV',
      leadUserId: 'ha',
      workflowTemplateId: 'org-kanban',
      members: [{ userId: 'khang', role: 'ScrumMaster' }],
      issueTypes: expect.arrayContaining([{ code: 'Story', name: 'Use case' }])
    }));
    expect(wizard.step()).toBe('done');
  });

  it('returns to step 1 with the server message when the key became taken', () => {
    const wizard = create();
    fillStep1(wizard);
    wizard.next();
    data.createProject.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { title: 'Mã HTQLTV đã được dùng trong tổ chức.', code: 'PROJECT_KEY_DUPLICATE' }
    })));
    wizard.submit();
    expect(wizard.step()).toBe(1);
    expect(wizard.keyError()).toBe('Mã HTQLTV đã được dùng trong tổ chức.');
  });

  it('asks before closing once something was entered', () => {
    const wizard = create();
    const dismissed = vi.fn();
    wizard.dismissed.subscribe(dismissed);
    wizard.onNameChange('Dự án');
    wizard.requestClose();
    expect(dismissed).not.toHaveBeenCalled();
    expect(wizard.confirmingClose()).toBe(true);
    // Esc / Đóng lần nữa khi đang hỏi thì quay lại nhập tiếp, không bỏ dữ liệu.
    wizard.requestClose();
    expect(wizard.confirmingClose()).toBe(false);
    expect(dismissed).not.toHaveBeenCalled();
  });

  it('closes without asking when nothing was entered', () => {
    const wizard = create();
    const dismissed = vi.fn();
    wizard.dismissed.subscribe(dismissed);
    wizard.requestClose();
    expect(dismissed).toHaveBeenCalledTimes(1);
  });
});
