import { Component, DestroyRef, ElementRef, HostListener, ViewChild, afterNextRender, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { filter } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { IdentityService } from '../../core/services/identity.service';
import { ProjectManagementService, Project, WorkItem } from '../../core/services/project-management.service';
import { ExcelDataService } from '../../core/services/excel-data.service';
import { ToastService, ToastMessage } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { TaskDetailDrawerComponent } from '../../shared/components/task-detail-drawer/task-detail-drawer';
import { CreateTaskModalComponent } from '../../shared/components/create-task-modal/create-task-modal';
import { CreateProjectModalComponent } from '../../shared/components/create-project-modal/create-project-modal';
import { ExcelImportModalComponent } from '../../shared/components/excel-import-modal/excel-import-modal';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog';
import { RouteTransitionLayerComponent } from '../../shared/motion/route-transition-layer/route-transition-layer';
import { MotionOrchestratorService } from '../../core/motion/motion-orchestrator.service';
import { FocusRecommendationService } from '../../core/attention/focus-recommendation.service';
import { RecentWorkService } from '../../core/attention/recent-work.service';
import { AvatarModalComponent } from '../../shared/components/avatar-modal/avatar-modal.component';
import { resolveAvatarUrl } from '../../core/utils/avatar.utils';
import { CreateOrgModalComponent } from '../../features/workspace/create-org/create-org-modal';
import { RecentListComponent } from '../../features/workspace/components/recent-list';
import { WorkspaceDataSource } from '../../features/workspace/data/workspace-data-source';
import { ORG_ROLE_LABELS, OrganizationSummary, RecentItem } from '../../features/workspace/data/workspace.models';
import { WorkspaceContextService } from '../../features/workspace/workspace-context.service';
import { WorkspaceNavigation } from '../../features/workspace/workspace-navigation';
import { QuickProfileModalComponent } from '../../shared/components/quick-profile-modal/quick-profile-modal.component';

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
    AvatarModalComponent,
    CreateOrgModalComponent,
    RecentListComponent,
    QuickProfileModalComponent
  ],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss'
})
export class AppShellComponent {
  @HostListener('window:beforeunload', ['$event'])
  protectPendingChanges(event: BeforeUnloadEvent): void {
    if (this.projectService.pendingSaves() > 0) event.preventDefault();
  }
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
  private readonly workspace = inject(WorkspaceContextService);
  private readonly workspaceData = inject(WorkspaceDataSource);
  private readonly workspaceNav = inject(WorkspaceNavigation);

  @ViewChild('pageBody', { read: ElementRef })
  private pageBody?: ElementRef<HTMLElement>;

  // Current Route Path Signal
  readonly currentUrl = signal<string>(this.router.url);

  // Check if current route is inside a Project Context (Show Sub-Header)
  readonly isProjectRoute = computed(() => {
    const url = this.currentUrl();
    if (!url || url === '/' || url.startsWith('/for-you') || url.startsWith('/orgs') || url.startsWith('/profile') || url.startsWith('/notifications') || url.startsWith('/admin') || url.startsWith('/auth') || url.startsWith('/login') || url.startsWith('/invite')) {
      return false;
    }
    return (
      url.startsWith('/project') ||
      url.startsWith('/projects') ||
      url.startsWith('/board') ||
      url.startsWith('/backlog') ||
      url.startsWith('/roadmap') ||
      url.startsWith('/task-list') ||
      url.startsWith('/list') ||
      url.startsWith('/documents') ||
      url.startsWith('/reports') ||
      url.startsWith('/issues')
    );
  });

  isTabActive(tab: string): boolean {
    const url = this.currentUrl();
    switch (tab) {
      case 'summary':
        return url.startsWith('/project/summary') || url.includes('/summary');
      case 'roadmap':
        return url.startsWith('/roadmap');
      case 'board':
        return url.startsWith('/board');
      case 'backlog':
        return url.startsWith('/backlog');
      case 'task-list':
        return url.startsWith('/task-list') || url.startsWith('/list');
      case 'documents':
        return url.startsWith('/documents') || url.includes('/documents');
      case 'reports':
        return url.startsWith('/reports');
      case 'members':
        return url.includes('/members') || url.includes('tab=members');
      case 'settings':
        return (url.includes('/settings') && !url.includes('tab=members')) || url.includes('/workflow') || url.includes('/boards') || url.includes('/board-settings');
      default:
        return false;
    }
  }

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

  readonly isCreateTaskModalOpen = signal<boolean>(false);
  readonly isCreateProjectModalOpen = signal<boolean>(false);
  readonly isExcelImportModalOpen = signal<boolean>(false);
  readonly isCreateOrgOpen = signal<boolean>(false);

  // Flyout Popups State
  readonly isRecentFlyoutOpen = signal<boolean>(false);
  readonly isQuickProfileOpen = signal<boolean>(false);

  openQuickProfile(): void {
    this.isQuickProfileOpen.set(true);
  }

  // Sidebar 2 cấp: trang chủ (Tổ chức) và tổ chức đang xem (Dự án, Cài đặt)
  readonly projectsList = this.projectService.allProjectsList;
  readonly activeProject = this.projectService.currentProject;
  readonly workItems = this.projectService.workItems;
  readonly organizations = this.workspace.organizations;
  readonly orgRoleLabels = ORG_ROLE_LABELS;
  readonly recentItems = signal<RecentItem[]>([]);
  /** Tổ chức đang xem: theo URL /orgs/:id, hoặc tổ chức của dự án đang mở khi ở trong màn dự án / công việc. */
  readonly currentOrgId = computed(() => {
    const url = this.currentUrl();
    const match = /^\/orgs\/([^/?#]+)/.exec(url);
    if (match) return decodeURIComponent(match[1]);
    if (this.isProjectRoute() || url.startsWith('/issues/')) {
      return this.activeProject().orgId || null;
    }
    // Giữ nguyên tổ chức nếu đang có dự án được chọn (tránh mất context khi xem hồ sơ hoặc trang khác)
    if (this.projectService.hasActiveProject() && this.activeProject()?.orgId) {
      return this.activeProject().orgId;
    }
    return null;
  });
  readonly sidebarOrg = computed(() => this.organizations().find(org => org.id === this.currentOrgId()) ?? null);

  readonly currentBoardTabTitle = computed(() => {
    const p = this.activeProject();
    // 1. Ưu tiên tên Tab / Tên Workflow mà người dùng đã chọn áp dụng
    if ((p as any)?.boardTabTitle) return (p as any).boardTabTitle;
    if ((p as any)?.workflowName) {
      const w = (p as any).workflowName.replace(/^Bảng\s+/i, '').trim();
      if (w) return w;
    }
    // 2. Tiếp theo từ tên Board thực tế trong cơ sở dữ liệu
    if ((p as any)?.boardName) {
      const trimmed = (p as any).boardName.replace(/^Bảng\s+/i, '').trim();
      if (trimmed) return trimmed;
    }
    // 3. Kiểm tra lưu vết trong localStorage hoặc sessionStorage theo dự án
    if (typeof localStorage !== 'undefined' && p?.id) {
      const saved = localStorage.getItem(`projectmgmt.workflow-name.${p.id}`);
      if (saved) return saved.replace(/^Bảng\s+/i, '').trim();
    }
    if (typeof sessionStorage !== 'undefined' && p?.id) {
      const saved = sessionStorage.getItem(`projectmgmt.workflow-name.${p.id}`);
      if (saved) return saved.replace(/^Bảng\s+/i, '').trim();
    }
    // 4. Fallback theo loại quy trình quản lý
    if ((p as any)?.managementType === 'Kanban' || p?.type === 'Kanban') return 'Kanban';
    if ((p as any)?.managementType === 'Scrum' || p?.type === 'Scrum') return 'Scrum';
    return 'Scrum';
  });

  /** Vai trò theo ngữ cảnh hiện tại (Dự án -> Tổ chức -> Tài khoản cá nhân, tuyệt đối không gán Admin toàn cục) */
  readonly currentContextRole = computed(() => {
    if (this.isProjectRoute() && this.activeProject().id) {
      const proj = this.activeProject();
      const myId = this.identity.authState().currentUser?.id;
      if (proj.leadUserId && myId && proj.leadUserId.toLowerCase() === myId.toLowerCase()) return 'Giám đốc dự án';
      const myMember = (proj.members || []).find(m => m.userId && myId && m.userId.toLowerCase() === myId.toLowerCase());
      if (myMember) {
        switch (myMember.role) {
          case 'Project Lead': return 'Quản lý dự án';
          case 'Scrum Master': return 'Scrum Master';
          case 'Developer': return 'Kỹ sư phát triển';
          case 'QA Engineer': return 'QA Engineer';
          case 'Viewer': return 'Người quan sát';
        }
      }
      return 'Thành viên dự án';
    }
    const org = this.sidebarOrg();
    if (org) {
      return this.orgRoleLabels[org.myRole] || 'Thành viên tổ chức';
    }
    return 'Tài khoản cá nhân';
  });

  readonly canManageActiveProject = computed(() => {
    if (this.identity.isSystemAdmin()) return true;
    const currentUser = this.identity.authState().currentUser;
    const myId = currentUser?.id;
    const myName = currentUser?.displayName;
    const myEmail = currentUser?.email;
    if (!myId) return false;

    const org = this.sidebarOrg();
    if (org && (org.myRole === 'Owner' || (org.ownerId && org.ownerId.toLowerCase() === myId.toLowerCase()))) {
      return true;
    }

    const proj = this.activeProject();
    if (!proj || !proj.id) return false;

    // Giám đốc dự án / Người phụ trách cao nhất (LeadUserId)
    if (proj.leadUserId && proj.leadUserId.toLowerCase() === myId.toLowerCase()) {
      return true;
    }

    // Kiểm tra vai trò trong dự án: chỉ Quản lý dự án / Scrum Master mới có quyền Cài đặt
    const members = proj.members || [];
    const myMember = members.find(m =>
      (m.userId && m.userId.toLowerCase() === myId.toLowerCase()) ||
      (m.email && myEmail && m.email.toLowerCase() === myEmail.toLowerCase()) ||
      (m.displayName && myName && m.displayName.toLowerCase() === myName.toLowerCase())
    );

    if (myMember) {
      const role = (myMember.role || '').toLowerCase();
      return role.includes('lead') || role.includes('manager') || role.includes('scrum') || role.includes('director') || role.includes('owner');
    }

    return false;
  });

  readonly canEditProjectTasks = computed(() => {
    if (this.canManageActiveProject()) return true;
    const myId = this.identity.authState().currentUser?.id;
    if (!myId) return false;
    const proj = this.activeProject();
    const myMember = (proj.members || []).find(m => m.userId && m.userId.toLowerCase() === myId.toLowerCase());
    return myMember ? myMember.role !== 'Viewer' : true;
  });

  @HostListener('document:keydown', ['$event'])
  onGlobalKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      const input = document.querySelector<HTMLInputElement>('.desktop-quick-search input');
      input?.focus();
    }
  }

  constructor() {
    this.workspace.refreshOrganizations().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ error: () => undefined });
    this.projectService.getProjects().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      error: () => this.toastService.error('Dự án', 'Không thể tải dự án. Mở danh sách dự án để thử lại.')
    });
    afterNextRender(() => this.revealActiveProjectTab());
    this.identity.getUnreadCount().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ error: () => {} });

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
    const url = this.currentUrl();
    if (url.includes('/settings') || url.includes('/workflow') || url.includes('/boards') || url.includes('/members')) {
      return;
    }
    window.requestAnimationFrame(() => {
      if (this.pageBody) {
        this.motion.animatePage(this.pageBody.nativeElement, url);
      }
    });
  }

  readonly isAvatarModalOpen = signal(false);
  readonly avatarLoadErrorUrl = signal<string | null>(null);

  readonly currentResolvedAvatarUrl = computed(() => {
    const url = resolveAvatarUrl(this.identity.authState().currentUser?.avatarUrl);
    if (url && this.avatarLoadErrorUrl() === url) return null;
    return url;
  });

  openCurrentUserAvatarModal(): void {
    this.isAvatarModalOpen.set(true);
  }

  useAvatarFallback(_event: Event): void {
    const url = resolveAvatarUrl(this.identity.authState().currentUser?.avatarUrl);
    if (url) {
      this.avatarLoadErrorUrl.set(url);
    }
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

  /** Cài đặt tổ chức chỉ hiện với người tạo tổ chức hoặc Quản trị viên hệ thống. */
  isOrgOwner(org: OrganizationSummary | null | undefined): boolean {
    if (!org) return false;
    if (this.identity.isSystemAdmin()) return true;
    const myId = this.identity.authState().currentUser?.id;
    if (!myId) return false;
    if (org.myRole === 'Owner') return true;
    if (org.ownerId && org.ownerId.toLowerCase() === myId.toLowerCase()) return true;
    return false;
  }

  projectsOf(orgId: string): Project[] {
    return this.projectsList().filter(project => project.orgId === orgId && !project.isDeleted);
  }

  openProject(project: Project): void {
    this.closeFlyouts();
    this.workspaceNav.openProject(project);
  }

  onOrgCreated(org: OrganizationSummary): void {
    this.isCreateOrgOpen.set(false);
    this.workspace.upsert(org);
    this.toastService.success('Đã tạo tổ chức', org.name);
    this.workspaceNav.openOrganization(org.id);
  }

  toggleRecentFlyout(): void {
    const opening = !this.isRecentFlyoutOpen();
    this.isRecentFlyoutOpen.set(opening);
    if (opening) {
      this.workspaceData.listRecent(this.sidebarOrg()?.id).subscribe({ next: items => this.recentItems.set(items), error: () => this.recentItems.set([]) });
    }
  }

  openRecent(item: RecentItem): void {
    this.closeFlyouts();
    this.workspaceNav.openRecent(item);
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

  openSearchResult(item: WorkItem): void {
    this.closeFlyouts();
    this.recentWork.rememberTask(item.id);
    this.projectService.activeDrawerTask.set(item);
  }

  openCreateTaskModal(): void {
    if (!this.projectService.hasActiveProject() || !this.projectService.currentProject().id) {
      this.toastService.info('Chưa Chọn Dự Án', 'Vui lòng chọn hoặc khởi tạo một dự án trước khi tạo công việc mới.');
      void this.router.navigate(['/projects/new']);
      return;
    }
    this.isCreateTaskModalOpen.set(true);
  }

  closeCreateTaskModal(): void {
    this.isCreateTaskModalOpen.set(false);
  }

  openCreateProjectModal(): void {
    void this.router.navigate(['/projects/new']);
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
    const proj = this.projectService.currentProject();
    const projectKey = proj.projectKey || 'PROJECT';
    const dateStr = new Date().toISOString().slice(0, 10).replace(/-/g, '');
    const filename = `${projectKey}_WorkItems_Export_${dateStr}.xlsx`;
    this.excelService.exportToExcel(
      this.projectService.workItems(),
      filename,
      this.projectService.sprints(),
      proj.name || projectKey
    );
    this.toastService.success('Xuất Dữ Liệu Excel', `Đã tải xuống bảng Excel: ${filename}`);
  }

  closeActiveDrawer(): void {
    this.projectService.activeDrawerTask.set(null);
  }

  logout(): void {
    this.confirmService.confirm({
      title: 'Xác Nhận Đăng Xuất',
      message: 'Bạn có chắc chắn muốn đăng xuất khỏi Hệ thống Quản lý Dự án Enterprise?',
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

  onToastClick(toast: ToastMessage): void {
    if (toast.actionUrl) {
      this.toastService.remove(toast.id);
      void this.router.navigateByUrl(toast.actionUrl);
    }
  }
}
