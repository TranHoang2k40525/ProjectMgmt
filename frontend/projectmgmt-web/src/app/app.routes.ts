import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'projects' },
  { path: 'auth', loadComponent: () => import('./features/auth/auth-page').then(m => m.AuthPage) },
  { path: 'projects', loadComponent: () => import('./features/projects/projects-page').then(m => m.ProjectsPage) },
  { path: 'backlog', loadComponent: () => import('./features/backlog/backlog-page').then(m => m.BacklogPage) },
  { path: 'board', loadComponent: () => import('./features/board/board-page').then(m => m.BoardPage) },
  { path: 'issues/:id', loadComponent: () => import('./features/issue-detail/issue-detail-page').then(m => m.IssueDetailPage) },
  { path: 'notifications', loadComponent: () => import('./features/notifications/notifications-page').then(m => m.NotificationsPage) },
  { path: 'reports', loadComponent: () => import('./features/reports/reports-page').then(m => m.ReportsPage) },
  { path: 'ai-dataops', loadComponent: () => import('./features/ai-dataops/ai-dataops-page').then(m => m.AiDataOpsPage) },
  { path: '**', redirectTo: 'projects' }
];
