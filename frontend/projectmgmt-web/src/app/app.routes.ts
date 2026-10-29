import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

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
      { path: 'projects', loadComponent: () => import('./features/projects/projects-page').then(m => m.ProjectsPage) },
      { path: 'projects/new', loadComponent: () => import('./features/projects/project-create-page').then(m => m.ProjectCreatePage) },
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
