import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ContextMenuComponent } from './context-menu.component';
import { ProjectManagementService, ProjectStatus } from '../../core/services/project-management.service';

describe('ContextMenuComponent & Active Workflow Statuses', () => {
  let fixture: ComponentFixture<ContextMenuComponent>;
  let component: ContextMenuComponent;
  let projectService: ProjectManagementService;

  const customStatuses: ProjectStatus[] = [
    { id: 's3', name: 'Hoàn thành', category: 'Done', colorHex: '#10B981', isInitial: false, isDone: true, orderIndex: 3 },
    { id: 's1', name: 'Chưa làm', category: 'ToDo', colorHex: '#64748B', isInitial: true, isDone: false, orderIndex: 1 },
    { id: 's2', name: 'Kiểm thử QA', category: 'InProgress', colorHex: '#8B5CF6', isInitial: false, isActiveWork: true, orderIndex: 2 }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ContextMenuComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(ContextMenuComponent);
    component = fixture.componentInstance;
    projectService = TestBed.inject(ProjectManagementService);
  });

  it('renders statuses dynamically from active project workflow ordered by orderIndex', () => {
    projectService.projectStatuses.set(customStatuses);
    component.visible = true;
    component.itemKey = 'QLDA-12';
    component.statuses = projectService.orderedStatuses();
    component.currentStatus = 'Kiểm thử QA';
    component.workflowName = 'Quy trình Tùy biến';
    fixture.detectChanges();

    const menuText = fixture.nativeElement.textContent as string;
    expect(menuText).toContain('Chưa làm');
    expect(menuText).toContain('Kiểm thử QA');
    expect(menuText).toContain('Hoàn thành');
    expect(menuText).not.toContain('To Do (Cần làm)');
    expect(menuText).not.toContain('In Progress (Đang làm)');
    expect(menuText).not.toContain('Code Review');

    const buttons: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('button'));
    const statusLabels = buttons
      .map(b => b.textContent?.replace('check', '').trim() ?? '')
      .filter(t => ['Chưa làm', 'Kiểm thử QA', 'Hoàn thành'].includes(t));
    expect(statusLabels).toEqual(['Chưa làm', 'Kiểm thử QA', 'Hoàn thành']);
  });

  it('emits exact workflow status name and closes when status option is clicked', () => {
    component.visible = true;
    component.statuses = customStatuses;
    component.currentStatus = 'Chưa làm';
    fixture.detectChanges();

    const actionSpy = vi.fn();
    const closeSpy = vi.fn();
    component.action.subscribe(actionSpy);
    component.menuClose.subscribe(closeSpy);

    const buttons: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('button'));
    const qaBtn = buttons.find(b => b.textContent?.includes('Kiểm thử QA'));
    expect(qaBtn).toBeTruthy();
    qaBtn!.click();

    expect(actionSpy).toHaveBeenCalledWith({ action: 'status', value: 'Kiểm thử QA' });
    expect(closeSpy).toHaveBeenCalled();
  });

  it('detects custom Done statuses dynamically via ProjectManagementService', () => {
    projectService.projectStatuses.set(customStatuses);
    expect(projectService.orderedStatuses().map(s => s.name)).toEqual(['Chưa làm', 'Kiểm thử QA', 'Hoàn thành']);
    expect(projectService.isDoneStatusName('Hoàn thành')).toBe(true);
    expect(projectService.isDoneStatusName('Kiểm thử QA')).toBe(false);
  });
});
