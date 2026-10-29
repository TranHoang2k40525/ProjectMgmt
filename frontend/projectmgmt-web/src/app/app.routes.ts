import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { orgOwnerGuard } from './features/workspace/guards/org-owner.guard';
import { WorkspaceContextService } from './features/workspace/workspace-context.service';

/** /projects và /projects/new đưa về tổ chức mở gần nhất; chưa có thì về trang chủ để chọn tổ chức. */
function projectsOfLastOrg(create: boolean) {
  return () => {
    const orgId = inject(WorkspaceContextService).lastOrgId();
    const router = inject(Router);
    return orgId
      ? router.createUrlTree(['/orgs', orgId, 'projects'], create ? { queryParams: { create: 1 } } : {})
      : router.createUrlTree(['/for-you']);
  };
}

export const routes: Routes = [
  // 1. Standalone Auth Route
  {
    path: 'auth',
    loadComponent: () => import('./features/auth/auth-page').then(m => m.AuthPageComponent)
  },
  {
    path: 'login',
    redirectTo: 'auth',
    pathMatch: 'full'
  },

  // 2. Authenticated AppShell Layout Group
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layouts/app-shell/app-shell').then(m => m.AppShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'for-you' },
      { path: 'for-you', loadComponent: () => import('./features/workspace/home/home-page').then(m => m.WorkspaceHomePageComponent) },
      { path: 'project/summary', loadComponent: () => import('./features/projects/project-summary-page').then(m => m.ProjectSummaryPageComponent) },
      { path: 'projects/:id/summary', loadComponent: () => import('./features/projects/project-summary-page').then(m => m.ProjectSummaryPageComponent) },
      { path: 'project/members', loadComponent: () => import('./features/projects/project-members-page').then(m => m.ProjectMembersPageComponent) },
      { path: 'projects/:id/members', loadComponent: () => import('./features/projects/project-members-page').then(m => m.ProjectMembersPageComponent) },
      { path: 'profile', loadComponent: () => import('./features/identity/profile-page').then(m => m.ProfilePageComponent) },
      { path: 'admin/identity', loadComponent: () => import('./features/identity/rbac-admin-page').then(m => m.RbacAdminPageComponent) },
      { path: 'projects', pathMatch: 'full', redirectTo: projectsOfLastOrg(false) },
      { path: 'projects/new', pathMatch: 'full', redirectTo: projectsOfLastOrg(true) },
      {
        path: 'orgs/:orgId',
        loadComponent: () => import('./features/workspace/org/org-page').then(m => m.OrgPageComponent),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'overview' },
          { path: 'overview', loadComponent: () => import('./features/workspace/org/org-overview').then(m => m.OrgOverviewComponent) },
          { path: 'projects', loadComponent: () => import('./features/workspace/org/org-projects').then(m => m.OrgProjectsComponent) },
          {
            path: 'settings',
            canActivate: [orgOwnerGuard],
            loadComponent: () => import('./features/workspace/org/settings/org-settings').then(m => m.OrgSettingsComponent),
            children: [
              { path: '', pathMatch: 'full', redirectTo: 'members' },
              { path: 'members', loadComponent: () => import('./features/workspace/org/settings/org-members-settings').then(m => m.OrgMembersSettingsComponent) },
              { path: 'workflows', loadComponent: () => import('./features/workspace/org/settings/org-workflow-templates').then(m => m.OrgWorkflowTemplatesComponent) }
            ]
          }
        ]
      },
      { path: 'invite/:token', loadComponent: () => import('./features/workspace/invite/invite-accept-page').then(m => m.InviteAcceptPageComponent) },
      { path: 'projects/:id/settings', loadComponent: () => import('./features/projects/project-settings-page').then(m => m.ProjectSettingsPage) },
      { path: 'projects/:id/settings/workflow', loadComponent: () => import('./features/projects/workflow-settings-page').then(m => m.WorkflowSettingsPage) },
      { path: 'projects/:id/workflow', loadComponent: () => import('./features/projects/workflow-settings-page').then(m => m.WorkflowSettingsPage) },
      { path: 'projects/:id/settings/board', loadComponent: () => import('./features/projects/board-settings-page').then(m => m.BoardSettingsPage) },
      { path: 'projects/:id/boards', loadComponent: () => import('./features/projects/board-settings-page').then(m => m.BoardSettingsPage) },
      { path: 'backlog', loadComponent: () => import('./features/backlog/backlog-page').then(m => m.BacklogPage) },
      { path: 'board', loadComponent: () => import('./features/board/board-page').then(m => m.BoardPage) },
      { path: 'task-list', loadComponent: () => import('./features/task-list/task-list-page').then(m => m.TaskListPageComponent) },
      { path: 'list', loadComponent: () => import('./features/task-list/task-list-page').then(m => m.TaskListPageComponent) },
      { path: 'roadmap', loadComponent: () => import('./features/roadmap/roadmap-page').then(m => m.RoadmapPageComponent) },
      { path: 'reports', loadComponent: () => import('./features/reports/reports-page').then(m => m.ReportsPageComponent) },
      { path: 'documents', loadComponent: () => import('./features/spec-hub/spec-hub-page').then(m => m.SpecHubPageComponent) },
      { path: 'projects/:id/documents', loadComponent: () => import('./features/spec-hub/spec-hub-page').then(m => m.SpecHubPageComponent) },
      { path: 'issues/:id', loadComponent: () => import('./features/issue-detail/issue-detail-page').then(m => m.IssueDetailPage) },
      { path: 'notifications', loadComponent: () => import('./features/notifications/notifications-page').then(m => m.NotificationsPageComponent) }
    ]
  },

  { path: '**', redirectTo: 'for-you' }
];
