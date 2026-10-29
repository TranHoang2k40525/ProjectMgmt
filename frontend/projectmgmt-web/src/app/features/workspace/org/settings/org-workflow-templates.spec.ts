import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ConfirmDialogService } from '../../../../core/services/confirm-dialog.service';
import { ToastService } from '../../../../core/services/toast.service';
import { WorkspaceDataSource } from '../../data/workspace-data-source';
import { WorkflowTemplate } from '../../data/workspace.models';
import { WorkspaceContextService } from '../../workspace-context.service';
import { OrgWorkflowTemplatesComponent } from './org-workflow-templates';
import { blankDraft, draftFrom, isDraftValid, moveStatus, validateDraft } from './workflow-template-draft';

const preset: WorkflowTemplate = {
  id: 'preset-kanban-simple', orgId: null, name: 'Kanban đơn giản', managementType: 'Kanban', isPreset: true, isDefault: true,
  statuses: [{ name: 'To Do', category: 'ToDo' }, { name: 'In Progress', category: 'InProgress' }, { name: 'Done', category: 'Done' }]
};

describe('workflow template draft helpers', () => {
  it('copies presets as new templates and keeps ids of organization templates', () => {
    expect(draftFrom(preset)).toMatchObject({ id: null, name: 'Kanban đơn giản (bản sao)', managementType: 'Kanban' });
    expect(draftFrom({ ...preset, id: 'org-1', isPreset: false }).id).toBe('org-1');
  });

  it('moves statuses within bounds', () => {
    const names = (list: { name: string }[]) => list.map(status => status.name);
    expect(names(moveStatus(preset.statuses, 2, -1))).toEqual(['To Do', 'Done', 'In Progress']);
    expect(names(moveStatus(preset.statuses, 0, -1))).toEqual(['To Do', 'In Progress', 'Done']);
  });

  it('requires a name, unique status names and both a to-do and a done status', () => {
    const draft = { ...blankDraft(), name: 'X', statuses: [{ name: 'Mới', category: 'ToDo' as const }, { name: 'mới', category: 'InProgress' as const }] };
    const errors = validateDraft(draft);
    expect(errors.name).not.toBe('');
    expect(errors.statuses).toEqual(['', 'Trùng với trạng thái khác.']);
    expect(errors.form).toContain('Hoàn thành');
    expect(isDraftValid({ ...blankDraft(), name: 'Scrum nhóm A' })).toBe(true);
  });
});

describe('OrgWorkflowTemplatesComponent', () => {
  const data = {
    listWorkflowTemplates: vi.fn(() => of([preset])),
    createWorkflowTemplate: vi.fn(),
    setDefaultWorkflowTemplate: vi.fn()
  };

  beforeEach(() => {
    Object.values(data).forEach(fn => fn.mockClear());
    TestBed.configureTestingModule({
      providers: [
        { provide: WorkspaceDataSource, useValue: data },
        { provide: ToastService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: ConfirmDialogService, useValue: { confirm: vi.fn() } },
        { provide: WorkspaceContextService, useValue: { currentOrg: signal({ id: 'org' }) } }
      ]
    });
  });

  function create(): OrgWorkflowTemplatesComponent {
    const page = TestBed.runInInjectionContext(() => new OrgWorkflowTemplatesComponent());
    page.ngOnInit();
    return page;
  }

  it('does not save an invalid draft and shows its errors', () => {
    const page = create();
    page.openNew();
    page.save();
    expect(page.showErrors()).toBe(true);
    expect(data.createWorkflowTemplate).not.toHaveBeenCalled();
  });

  it('creates a template from a preset copy and adds it to the list', () => {
    const saved: WorkflowTemplate = { ...preset, id: 'org-1', orgId: 'org', name: 'Kanban hỗ trợ', isPreset: false, isDefault: false };
    data.createWorkflowTemplate.mockReturnValue(of(saved));
    const page = create();
    page.openFrom(preset);
    page.updateDraft({ name: 'Kanban hỗ trợ' });
    page.renameStatus(1, 'Đang xử lý');
    page.save();
    expect(data.createWorkflowTemplate).toHaveBeenCalledWith('org', {
      name: 'Kanban hỗ trợ',
      managementType: 'Kanban',
      statuses: [{ name: 'To Do', category: 'ToDo' }, { name: 'Đang xử lý', category: 'InProgress' }, { name: 'Done', category: 'Done' }]
    });
    expect(page.custom().map(template => template.id)).toEqual(['org-1']);
    expect(page.draft()).toBeNull();
  });

  it('marks only the chosen template as default', () => {
    const other: WorkflowTemplate = { ...preset, id: 'org-2', isPreset: false, isDefault: false };
    data.listWorkflowTemplates.mockReturnValue(of([preset, other]));
    data.setDefaultWorkflowTemplate.mockReturnValue(of({ ...other, isDefault: true }));
    const page = create();
    page.setDefault(other);
    expect(page.templates().map(template => template.isDefault)).toEqual([false, true]);
  });
});
