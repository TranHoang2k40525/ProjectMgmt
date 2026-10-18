import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectManagementService, ProjectMember } from '../../core/services/project-management.service';

@Component({
  selector: 'app-project-members-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="project-members-page flex flex-col gap-6 animate-fade-in">
      
      <!-- Top Action Bar -->
      <div class="members-header flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div class="flex flex-col">
          <h1 class="text-xl font-extrabold text-slate-900 dark:text-white flex items-center gap-2">
            <span class="material-symbols-outlined text-[24px] text-primary">group</span>
            Quản Lý Thành Viên & Phân Quyền Vai Trò
          </h1>
          <p class="text-xs text-slate-500">Quản lý danh sách thành viên trong dự án HUCE AI Lab Project và cấp quyền truy cập</p>
        </div>

        <button (click)="isInviteModalOpen = true" class="px-4 py-2 rounded-xl bg-primary hover:bg-primary-container text-white font-bold text-xs shadow-sm transition-all flex items-center gap-1.5">
          <span class="material-symbols-outlined text-[18px]">person_add</span>
          Thêm thành viên mới
        </button>
      </div>

      <!-- Members Grid List -->
      <div class="members-grid grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        @for (member of currentProject().members; track member.id) {
          <div class="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-sm flex flex-col justify-between gap-4 hover:shadow-md transition-shadow">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-primary text-white flex items-center justify-center font-bold text-sm">
                {{ member.displayName[0] }}
              </div>
              <div class="flex flex-col">
                <span class="text-sm font-bold text-slate-900 dark:text-white">{{ member.displayName }}</span>
                <span class="text-xs text-slate-400">{{ member.email }}</span>
              </div>
            </div>

            <div class="flex items-center justify-between pt-3 border-t border-slate-100 dark:border-slate-800">
              <span class="px-2.5 py-1 rounded-lg text-xs font-bold bg-indigo-50 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-900">
                {{ member.role }}
              </span>
              <span class="text-[10px] text-slate-400">Tham gia: {{ member.joinedAt }}</span>
            </div>
          </div>
        }
      </div>

      <!-- Invite Member Modal -->
      @if (isInviteModalOpen) {
        <div class="members-modal-backdrop fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs animate-fade-in">
          <div class="members-modal w-full max-w-md bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col p-6 gap-4">
            <h3 class="text-base font-bold text-slate-900 dark:text-white">Thêm Thành Viên Vào Dự Án</h3>

            <div class="flex flex-col gap-1.5">
              <label for="member-name" class="text-xs font-bold text-slate-500">Họ và tên</label>
              <input id="member-name" type="text" [(ngModel)]="newMemberName" placeholder="Nhập tên thành viên..." class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs focus:outline-none focus:border-primary" />
            </div>

            <div class="flex flex-col gap-1.5">
              <label for="member-email" class="text-xs font-bold text-slate-500">Email sinh viên / giảng viên HUCE</label>
              <input id="member-email" type="email" [(ngModel)]="newMemberEmail" placeholder="nguyenvana@huce.edu.vn" class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs focus:outline-none focus:border-primary" />
            </div>

            <div class="flex flex-col gap-1.5">
              <label for="member-role" class="text-xs font-bold text-slate-500">Vai trò trong dự án</label>
              <select id="member-role" [(ngModel)]="newMemberRole" class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs focus:outline-none focus:border-primary">
                <option value="Developer">Developer (Phát triển)</option>
                <option value="Scrum Master">Scrum Master (Quản trị Sprint)</option>
                <option value="QA Engineer">QA Engineer (Kiểm thử)</option>
                <option value="Project Lead">Project Lead (Trưởng dự án)</option>
                <option value="Viewer">Viewer (Xem báo cáo)</option>
              </select>
            </div>

            <div class="flex items-center justify-end gap-3 mt-2">
              <button (click)="isInviteModalOpen = false" class="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400">Hủy</button>
              <button (click)="submitInvite()" class="px-5 py-2 rounded-xl bg-primary text-white text-xs font-bold">Thêm thành viên</button>
            </div>
          </div>
        </div>
      }

    </div>
  `,
  styles: [`
    @media (max-width: 1279px) {
      .project-members-page, .members-header > div { min-width: 0; }
      .members-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .members-modal { max-height: calc(100dvh - 2rem); overflow-y: auto; }
    }

    @media (max-width: 767px) {
      .project-members-page { gap: 1rem; }
      .members-header { align-items: stretch; }
      .members-header > button { justify-content: center; min-height: 44px; width: 100%; }
      .members-grid { grid-template-columns: minmax(0, 1fr); gap: .75rem; }
      .members-modal-backdrop { align-items: flex-end; padding: 0; }
      .members-modal { max-height: calc(100dvh - 1rem); border-radius: 1rem 1rem 0 0; padding: 1rem; }
      .members-modal > div:last-child { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .members-modal > div:last-child button { min-height: 44px; }
    }

    @media (max-width: 932px) and (orientation: landscape) and (max-height: 520px) {
      .members-modal-backdrop { align-items: stretch; padding: .5rem; }
      .members-modal { max-width: 42rem; max-height: calc(100dvh - 1rem); margin: auto; }
    }
  `]
})
export class ProjectMembersPageComponent {
  private readonly projectService = inject(ProjectManagementService);
  readonly currentProject = this.projectService.currentProject;

  isInviteModalOpen = false;
  newMemberName = '';
  newMemberEmail = '';
  newMemberRole: ProjectMember['role'] = 'Developer';

  submitInvite(): void {
    if (this.newMemberName.trim() && this.newMemberEmail.trim()) {
      this.projectService.addProjectMember({
        displayName: this.newMemberName,
        email: this.newMemberEmail,
        role: this.newMemberRole
      });
      this.isInviteModalOpen = false;
      this.newMemberName = '';
      this.newMemberEmail = '';
    }
  }
}
