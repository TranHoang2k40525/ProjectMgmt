import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { IdentityService } from '../services/identity.service';
import { ProjectManagementService } from '../services/project-management.service';
import { ToastService } from '../services/toast.service';
import { WorkspaceContextService } from '../../features/workspace/workspace-context.service';

export function findProjectId(route: ActivatedRouteSnapshot): string | null {
  for (const snapshot of [...route.pathFromRoot].reverse()) {
    const id = snapshot.paramMap.get('id') || snapshot.paramMap.get('projectId');
    if (id) return id;
  }
  return null;
}

/**
 * Chỉ Quản lý dự án trở lên (Chủ tổ chức, Giám đốc dự án, Scrum Master, Admin)
 * mới có quyền truy cập Cài đặt dự án & cấu hình Workflow/Board.
 * Các vai trò thực thi (Developer, QA, Viewer) sẽ bị chặn và đưa về trang Tổng quan dự án.
 */
export const projectManagerGuard: CanActivateFn = route => {
  const router = inject(Router);
  const identity = inject(IdentityService);
  const projectService = inject(ProjectManagementService);
  const workspaceContext = inject(WorkspaceContextService);
  const toast = inject(ToastService);

  if (identity.isSystemAdmin()) return true;

  const currentUserId = identity.authState().currentUser?.id;
  const currentUserEmail = identity.authState().currentUser?.email;
  const currentUserName = identity.authState().currentUser?.displayName;
  if (!currentUserId) {
    return router.createUrlTree(['/auth']);
  }

  const projectId = findProjectId(route) || projectService.currentProject().id;
  if (!projectId) {
    return router.createUrlTree(['/for-you']);
  }

  // 1. Kiểm tra quyền chủ tổ chức
  const currentOrg = workspaceContext.currentOrg();
  if (currentOrg && (currentOrg.ownerId === currentUserId || (currentOrg as any).myRole === 'Owner')) {
    return true;
  }

  // 2. Kiểm tra dự án trong cache
  const cachedProj = projectService.currentProject();
  if (cachedProj && cachedProj.id === projectId) {
    if (cachedProj.leadUserId && cachedProj.leadUserId.toLowerCase() === currentUserId.toLowerCase()) {
      return true;
    }
    const myMember = (cachedProj.members || []).find(m =>
      (m.userId && m.userId.toLowerCase() === currentUserId.toLowerCase()) ||
      (m.email && currentUserEmail && m.email.toLowerCase() === currentUserEmail.toLowerCase()) ||
      (m.displayName && currentUserName && m.displayName.toLowerCase() === currentUserName.toLowerCase())
    );
    if (myMember) {
      const r = (myMember.role || '').toLowerCase();
      if (r.includes('lead') || r.includes('manager') || r.includes('scrum') || r.includes('director') || r.includes('owner')) {
        return true;
      }
      toast.warning('Phân quyền', 'Chỉ Quản lý dự án trở lên mới có quyền truy cập Cài đặt dự án.');
      return router.createUrlTree(['/projects', projectId, 'summary']);
    }
  }

  // 3. Nếu chưa có thông tin đầy đủ, tải thành viên dự án để xác thực
  return projectService.loadProjectMembers(projectId).pipe(
    map(members => {
      const myMember = members.find(m =>
        (m.userId && m.userId.toLowerCase() === currentUserId.toLowerCase()) ||
        (m.email && currentUserEmail && m.email.toLowerCase() === currentUserEmail.toLowerCase()) ||
        (m.displayName && currentUserName && m.displayName.toLowerCase() === currentUserName.toLowerCase())
      );
      if (myMember) {
        const r = (myMember.role || '').toLowerCase();
        if (r.includes('lead') || r.includes('manager') || r.includes('scrum') || r.includes('director') || r.includes('owner')) {
          return true;
        }
      }
      toast.warning('Phân quyền', 'Chỉ Quản lý dự án trở lên mới có quyền truy cập Cài đặt dự án.');
      return router.createUrlTree(['/projects', projectId, 'summary']);
    }),
    catchError(() => {
      toast.warning('Phân quyền', 'Chỉ Quản lý dự án trở lên mới có quyền truy cập Cài đặt dự án.');
      return of(router.createUrlTree(['/projects', projectId, 'summary']));
    })
  );
};
