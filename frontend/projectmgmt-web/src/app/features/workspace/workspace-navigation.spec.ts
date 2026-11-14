import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ProjectManagementService } from '../../core/services/project-management.service';
import { ToastService } from '../../core/services/toast.service';
import { WorkspaceContextService } from './workspace-context.service';
import { WorkspaceNavigation } from './workspace-navigation';

describe('WorkspaceNavigation', () => {
  const navigate = vi.fn(() => Promise.resolve(true));
  const selectProject = vi.fn();
  const getProjectById = vi.fn();
  const toastError = vi.fn();
  const known = { id: 'p-1', name: 'Học tập' };

  beforeEach(() => {
    [navigate, selectProject, getProjectById, toastError].forEach(fn => fn.mockClear());
    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: { navigate } },
        { provide: WorkspaceContextService, useValue: { recordVisit: vi.fn() } },
        { provide: ToastService, useValue: { error: toastError } },
        { provide: ProjectManagementService, useValue: { allProjectsList: signal([known]), selectProject, getProjectById } }
      ]
    });
  });

  it('selects the real project before opening its summary', () => {
    TestBed.inject(WorkspaceNavigation).openProject({ id: 'p-1' });
    expect(selectProject).toHaveBeenCalledWith(known);
    expect(navigate).toHaveBeenCalledWith(['/project/summary']);
  });

  it('loads an unknown project first and explains when it cannot be opened', () => {
    getProjectById.mockReturnValueOnce(of({ id: 'p-2' })).mockReturnValueOnce(throwError(() => new Error('404')));
    const nav = TestBed.inject(WorkspaceNavigation);
    nav.openProject({ id: 'p-2' });
    expect(selectProject).toHaveBeenCalledWith({ id: 'p-2' });
    nav.openProject({ id: 'p-gone' });
    expect(toastError).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledTimes(1);
  });
});
