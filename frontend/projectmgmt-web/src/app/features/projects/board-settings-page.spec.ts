import { TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { BoardSettingsPage } from './board-settings-page';
import { ProjectManagementService } from '../../core/services/project-management.service';

describe('Board settings persistence feedback', () => {
  let response: Subject<boolean>;
  let updateResponse: Subject<import('../../core/services/project-management.service').BoardColumn>;
  const reorder = vi.fn(() => response);
  const update = vi.fn(() => updateResponse);
  const columns = [
    { id: 'first', boardId: 'board', statusId: 'todo', statusName: 'Todo', orderIndex: 1 },
    { id: 'second', boardId: 'board', statusId: 'done', statusName: 'Done', orderIndex: 2 }
  ];

  beforeEach(() => {
    reorder.mockClear();
    update.mockClear();
    response = new Subject();
    updateResponse = new Subject();
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: {} },
      { provide: ProjectManagementService, useValue: { reorderBoardColumns: reorder, updateBoardColumn: update } }
    ] });
  });

  it('keeps saved values on failure and keeps the draft for retry', () => {
    const page = TestBed.runInInjectionContext(() => new BoardSettingsPage());
    page.columns.set(columns);
    page.editColumn(columns[0]);
    page.editName = 'New name';
    page.editWip = 3;
    page.saveColumn();
    page.saveColumn();
    expect(update).toHaveBeenCalledTimes(1);
    expect(page.columns()[0].name).toBeUndefined();
    updateResponse.error({ detail: 'Save failed' });
    expect(page.savingColumn()).toBe(false);
    expect(page.editingColumnId()).toBe('first');
    expect(page.editName).toBe('New name');
    expect(page.alertMessage()).toBe('Save failed');
  });

  it('applies the server result only after success', () => {
    const page = TestBed.runInInjectionContext(() => new BoardSettingsPage());
    page.columns.set(columns);
    page.editColumn(columns[0]);
    page.editName = '  New name  ';
    page.editWip = null;
    page.saveColumn();
    expect(update).toHaveBeenCalledWith('first', { name: 'New name', wipLimit: null });
    updateResponse.next({ ...columns[0], name: 'New name', wipLimit: null });
    expect(page.columns()[0].name).toBe('New name');
    expect(page.editingColumnId()).toBeNull();
  });

  it.each([0, -1, 1.5])('rejects invalid WIP %s without HTTP', wip => {
    const page = TestBed.runInInjectionContext(() => new BoardSettingsPage());
    page.editColumn(columns[0]);
    page.editWip = wip;
    page.saveColumn();
    expect(update).not.toHaveBeenCalled();
  });

  it('cancel does not alter persisted values', () => {
    const page = TestBed.runInInjectionContext(() => new BoardSettingsPage());
    page.columns.set(columns);
    page.editColumn(columns[0]);
    page.editName = 'Draft';
    page.cancelEdit();
    expect(page.editingColumnId()).toBeNull();
    expect(page.columns()).toEqual(columns);
    expect(update).not.toHaveBeenCalled();
  });

  it('does not show an unsaved order and blocks overlapping reorder requests', () => {
    const page = TestBed.runInInjectionContext(() => new BoardSettingsPage());
    page.selectedBoard.set({ id: 'board', projectId: 'aa', name: 'Board', type: 'Kanban', isDefault: true, createdAt: '' });
    page.columns.set(columns);
    page.moveColumn(0, 1);
    expect(reorder).toHaveBeenCalledWith('board', ['second', 'first']);
    expect(page.columns().map(c => c.id)).toEqual(['first', 'second']);
    page.moveColumn(0, 1);
    expect(reorder).toHaveBeenCalledTimes(1);
    response.error({ detail: 'Order was rejected' });
    expect(page.savingOrder()).toBe(false);
    expect(page.alertMessage()).toBe('Order was rejected');
    expect(page.columns().map(c => c.id)).toEqual(['first', 'second']);
  });
});
