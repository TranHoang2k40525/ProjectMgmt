import { Component, DestroyRef, ElementRef, HostListener, OnDestroy, ViewChild, afterNextRender, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { filter } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { IdentityService } from '../../core/services/identity.service';
import { ProjectManagementService, Project, WorkItem } from '../../core/services/project-management.service';
import { ExcelDataService } from '../../core/services/excel-data.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { TaskDetailDrawerComponent } from '../../shared/components/task-detail-drawer/task-detail-drawer';
import { CreateTaskModalComponent } from '../../shared/components/create-task-modal/create-task-modal';
import { CreateProjectModalComponent } from '../../shared/components/create-project-modal/create-project-modal';
import { ExcelImportModalComponent } from '../../shared/components/excel-import-modal/excel-import-modal';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog';
import { RouteTransitionLayerComponent } from '../../shared/motion/route-transition-layer/route-transition-layer';
import { MotionOrchestratorService } from '../../core/motion/motion-orchestrator.service';
import { MotionDirective } from '../../shared/motion/motion.directive';
import { FocusRecommendationService } from '../../core/attention/focus-recommendation.service';
import { RecentWorkService } from '../../core/attention/recent-work.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TaskDetailDrawerComponent,
    CreateTaskModalComponent,
    CreateProjectModalComponent,
    ExcelImportModalComponent,
    ConfirmDialogComponent,
    RouteTransitionLayerComponent,
    MotionDirective
  ],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss'
})
export class AppShellComponent implements OnDestroy {
  readonly identity = inject(IdentityService);
  readonly focus = inject(FocusRecommendationService);
  readonly projectService = inject(ProjectManagementService);
  readonly toastService = inject(ToastService);
  readonly confirmService = inject(ConfirmDialogService);
  private readonly excelService = inject(ExcelDataService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly motion = inject(MotionOrchestratorService);
  private readonly recentWork = inject(RecentWorkService);

  @ViewChild('pageBody', { read: ElementRef })
  private pageBody?: ElementRef<HTMLElement>;

  // Current Route Path Signal
  readonly currentUrl = signal<string>(this.router.url);

  // Check if current route is inside a Project Context (Show Sub-Header)
  readonly isProjectRoute = computed(() => {
    const url = this.currentUrl();
    return url.includes('/project/') || url.includes('/board') || url.includes('/backlog') || url.includes('/task-list') || url.includes('/roadmap') || url.includes('/reports');
  });

  // Header Live Search Signal
  readonly headerSearchQuery = signal<string>('');
  readonly isMobileSearchOpen = signal<boolean>(false);
  readonly headerSearchResults = computed(() => {
    const query = this.headerSearchQuery().trim().toLowerCase();
    if (!query) return [];
    return this.projectService.workItems().filter(i => 
      i.title.toLowerCase().includes(query) || i.issueKey.toLowerCase().includes(query)
    ).slice(0, 5);
  });

  // Global Interactive UI Signals
  readonly isSidebarCollapsed = signal<boolean>(typeof window !== 'undefined' && window.innerWidth < 1280);

  // Computed Expanded State
  readonly isSidebarExpanded = computed(() => !this.isSidebarCollapsed());

  readonly isSpacesSectionExpanded = signal<boolean>(true);
  readonly isCreateTaskModalOpen = signal<boolean>(false);
  readonly isCreateProjectModalOpen = signal<boolean>(false);
  readonly isExcelImportModalOpen = signal<boolean>(false);

  // Flyout Popups State
  readonly isRecentFlyoutOpen = signal<boolean>(false);

  // Projects & Recent Work Items
  readonly projectsList = this.projectService.allProjectsList;
  readonly activeProject = this.projectService.currentProject;
  readonly workItems = this.projectService.workItems;

  constructor() {
    afterNextRender(() => this.revealActiveProjectTab());

    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((event) => {
      this.currentUrl.set(event.urlAfterRedirects || event.url);
      this.closeFlyouts();
      // Auto-collapse sidebar on mobile/tablet viewports on navigation
      if (window.innerWidth < 1280) {
        this.isSidebarCollapsed.set(true);
      }
      this.revealActiveProjectTab();
    });
  }

  onRouteActivated(): void {
    window.requestAnimationFrame(() => {
      if (this.pageBody) {
        this.motion.animatePage(this.pageBody.nativeElement, this.currentUrl());
      }
    });
  }

  ngOnDestroy(): void {
    this.motion.destroy();
  }

  toggleSidebar(): void {
    this.isMobileSearchOpen.set(false);
    this.isRecentFlyoutOpen.set(false);
    this.isSidebarCollapsed.update(val => !val);
  }

  toggleMobileSearch(): void {
    this.isRecentFlyoutOpen.set(false);
    this.isSidebarCollapsed.set(true);
    this.isMobileSearchOpen.update(value => !value);
  }

  toggleSpacesSection(): void {
    this.isSpacesSectionExpanded.update(val => !val);
  }

  toggleRecentFlyout(): void {
    this.isRecentFlyoutOpen.update(val => !val);
  }

  closeFlyouts(): void {
    this.isRecentFlyoutOpen.set(false);
    this.isMobileSearchOpen.set(false);
    this.headerSearchQuery.set('');
  }

  @HostListener('document:keydown.escape')
  closeTransientNavigation(): void {
    this.closeFlyouts();
    if (typeof window !== 'undefined' && window.innerWidth < 1280) {
      this.isSidebarCollapsed.set(true);
    }
  }

  private revealActiveProjectTab(): void {
    if (typeof window === 'undefined' || !window.matchMedia('(max-width: 1279px)').matches) return;

    window.requestAnimationFrame(() => {
      const activeTab = document.querySelector<HTMLElement>('.project-context-tabs a[aria-current="page"]');
      activeTab?.scrollIntoView({ behavior: 'auto', block: 'nearest', inline: 'center' });
    });
  }

  selectProjectSpace(proj: Project): void {
    this.projectService.currentProject.set(proj);
    this.closeFlyouts();
    this.toastService.info('Chuyển Không Gian Dự Án', `Đã mở không gian làm việc ${proj.name} (${proj.projectKey})`);
    this.router.navigate(['/project/summary']);
  }

  openSearchResult(item: WorkItem): void {
    this.recentWork.rememberTask(item.id);
    this.projectService.activeDrawerTask.set(item);
    this.closeFlyouts();
  }

  openRecentItem(item: WorkItem): void {
    this.recentWork.rememberTask(item.id);
    this.projectService.activeDrawerTask.set(item);
    this.closeFlyouts();
  }

  openCreateTaskModal(): void {
    this.isCreateTaskModalOpen.set(true);
  }

  closeCreateTaskModal(): void {
    this.isCreateTaskModalOpen.set(false);
  }

  openCreateProjectModal(): void {
    this.isCreateProjectModalOpen.set(true);
    this.closeFlyouts();
  }

  closeCreateProjectModal(): void {
    this.isCreateProjectModalOpen.set(false);
  }

  openExcelImportModal(): void {
    this.isExcelImportModalOpen.set(true);
  }

  closeExcelImportModal(): void {
    this.isExcelImportModalOpen.set(false);
  }

  triggerAiBreakdown(): void {
    this.toastService.info('AI Assistant Phân Rã', 'Đang tự động phân rã các User Stories trong Backlog thành các Subtasks chuẩn Agile!');
  }

  exportExcel(): void {
    this.excelService.exportToExcel(this.projectService.workItems(), 'HUCE_Project_WorkItems_Export.csv');
    this.toastService.success('Xuất Dữ Liệu Excel', 'Đã tải xuống file CSV/Excel danh sách công việc thành công.');
  }

  closeActiveDrawer(): void {
    this.projectService.activeDrawerTask.set(null);
  }

  logout(): void {
    this.confirmService.confirm({
      title: 'Xác Nhận Đăng Xuất',
      message: 'Bạn có chắc chắn muốn đăng xuất khỏi hệ thống HUCE Scrum Platform?',
      type: 'danger',
      confirmText: 'Đăng xuất ngay',
      cancelText: 'Hủy bỏ',
      onConfirm: () => {
        this.identity.logout();
        this.toastService.info('Đăng Xuất Thành Công', 'Tài khoản của bạn đã được đăng xuất an toàn.');
        this.router.navigate(['/auth']);
      }
    });
  }
}
