import { apiErrorMessage } from '../../core/http/api-error-message';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IdentityService } from '../../core/services/identity.service';
import {
  CustomIssueType,
  Project,
  ProjectLabel,
  ProjectManagementService,
  ProjectPriority,
  PriorityTemplate,
  PriorityTemplateItem,
  UpdateProjectRequest,
  UserDisplayInfo
} from '../../core/services/project-management.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { ToastService } from '../../core/services/toast.service';
import { WorkspaceDataSource } from '../workspace/data/workspace-data-source';
import { WorkspaceContextService } from '../workspace/workspace-context.service';
import { WorkflowSettingsPage } from './workflow-settings-page';
import { BoardSettingsPage } from './board-settings-page';
import { ProjectMembersPageComponent } from './project-members-page';

export type ProjectSettingsTab = 'general' | 'issue-types' | 'labels' | 'priorities' | 'workflow' | 'board' | 'members';

@Component({
  selector: 'app-project-settings-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    WorkflowSettingsPage,
    BoardSettingsPage,
    ProjectMembersPageComponent
  ],
  template: `
    <div class="settings-container">
      <div class="breadcrumb">
        <a routerLink="/projects">Dự án</a> &gt;
        @if (project()) {
          <a routerLink="/project/summary" class="hover:text-primary hover:underline">{{ project()?.name }} ({{ project()?.projectKey }})</a> &gt;
        }
        <span>Cài đặt</span>
      </div>

      <div class="settings-header">
        <div>
          <h1>Cài đặt Dự án</h1>
          <p class="subtitle">Quản lý cấu hình thông tin chung, loại công việc động, nhãn dán, quy trình (Workflow) và bảng (Board)</p>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="settings-tabs">
        @if (canManageProject()) {
        <button
          type="button"
          (click)="selectTab('general')"
          [class.active]="activeTab() === 'general'"
          class="tab-item"
        >
          <span class="material-symbols-outlined text-[18px]">tune</span>
          <span>Thông tin chung</span>
        </button>
        <button
          type="button"
          (click)="selectTab('issue-types')"
          [class.active]="activeTab() === 'issue-types'"
          class="tab-item"
        >
          <span class="material-symbols-outlined text-[18px]">category</span>
          <span>Loại công việc (Issue Types)</span>
        </button>
        <button
          type="button"
          (click)="selectTab('labels')"
          [class.active]="activeTab() === 'labels'"
          class="tab-item"
        >
          <span class="material-symbols-outlined text-[18px]">label</span>
          <span>Quản lý Nhãn (Labels)</span>
        </button>
        <button
          type="button"
          (click)="selectTab('priorities')"
          [class.active]="activeTab() === 'priorities'"
          class="tab-item"
        >
          <span class="material-symbols-outlined text-[18px]">flag</span>
          <span>Mức độ ưu tiên (Priorities)</span>
        </button>
        <button
          type="button"
          (click)="selectTab('workflow')"
          [class.active]="activeTab() === 'workflow'"
          class="tab-item"
        >
          <span class="material-symbols-outlined text-[18px]">sync_alt</span>
          <span>Cấu hình Workflow &amp; Board</span>
        </button>
        }
        <button
          type="button"
          (click)="selectTab('members')"
          [class.active]="activeTab() === 'members'"
          class="tab-item"
        >
          <span class="material-symbols-outlined text-[18px]">badge</span>
          <span>Thành viên &amp; Phân quyền</span>
        </button>
      </div>

      @if (loading()) {
        <div class="loading-box">
          <div class="spinner"></div>
          <p>Đang tải dữ liệu cài đặt dự án...</p>
        </div>
      } @else if (error()) {
        <div class="alert alert-error">
          {{ error() }}
        </div>
      } @else {
        <!-- Chỉ Quản lý dự án trở lên mới được xem cấu hình -->
        @if (!canManageProject() && activeTab() !== 'members') {
          <div class="p-8 my-6 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-amber-900 dark:text-amber-200 flex flex-col items-center text-center gap-3 animate-fade-in">
            <span class="material-symbols-outlined text-[42px] text-amber-600">lock</span>
            <h3 class="text-base font-bold">Chỉ dành cho Quản lý dự án</h3>
            <p class="text-xs max-w-md text-amber-800 dark:text-amber-300">
              Bạn đang ở vai trò không có quyền cấu hình dự án. Chỉ Giám đốc dự án, Quản lý dự án hoặc Scrum Master mới có thể xem và chỉnh sửa thông tin này.
            </p>
            <button type="button" (click)="selectTab('members')" class="mt-2 inline-flex items-center gap-1.5 px-4 py-2 rounded-xl bg-slate-900 dark:bg-amber-600 text-white text-xs font-bold transition-all cursor-pointer">
              <span class="material-symbols-outlined text-[16px]">badge</span>
              <span>Xem danh sách thành viên</span>
            </button>
          </div>
        }

        <!-- TAB 1: THÔNG TIN CHUNG -->
        @if (canManageProject() && activeTab() === 'general' && project()) {
          <div class="settings-content">
            <div class="card">
              <h2>Chỉnh sửa thông tin cơ bản</h2>
              <form (ngSubmit)="onSaveGeneral()">
                <div class="form-group">
                  <label for="projectKeyDisabled">Mã dự án (Project Key)</label>
                  <input
                    type="text"
                    id="projectKeyDisabled"
                    [value]="project()?.projectKey"
                    disabled
                    class="form-control readonly-input"
                  />
                  <small class="form-hint">Mã Key là định danh bất biến, không thể thay đổi sau khi tạo.</small>
                </div>

                <div class="form-group">
                  <label for="name">Tên dự án <span class="required">*</span></label>
                  <input
                    type="text"
                    id="name"
                    name="name"
                    [(ngModel)]="editForm.name"
                    required
                    maxlength="200"
                    class="form-control"
                  />
                </div>

                <div class="form-group">
                  <label for="description">Mô tả dự án</label>
                  <textarea
                    id="description"
                    name="description"
                    [(ngModel)]="editForm.description"
                    rows="3"
                    class="form-control"
                  ></textarea>
                </div>

                <div class="form-group">
                  <label for="lead">Người phụ trách (Lead)</label>
                  <select
                    id="lead"
                    name="lead"
                    [(ngModel)]="editForm.leadUserId"
                    class="form-control"
                  >
                    @for (user of users(); track user.userId) {
                      <option [value]="user.userId">{{ user.displayName }}</option>
                    }
                  </select>
                </div>

                <div class="form-actions">
                  <button type="submit" [disabled]="saving()" class="btn btn-primary">
                    {{ saving() ? 'Đang lưu...' : 'Lưu thay đổi' }}
                  </button>
                </div>
              </form>
            </div>

            <!-- Danger Zone -->
            <div class="card danger-card">
              <h3>Khu vực nguy hiểm</h3>
              <p>Xóa mềm dự án sẽ ẩn dự án khỏi danh sách làm việc và giữ lại dữ liệu trong cơ sở dữ liệu.</p>
              <button (click)="onDeleteProject()" class="btn btn-danger">Xóa Dự Án Này</button>
            </div>
          </div>
        }

        <!-- TAB 2: QUẢN LÝ LOẠI CÔNG VIỆC DỘNG (ISSUE TYPES) -->
        @if (canManageProject() && activeTab() === 'issue-types') {
          <div class="settings-content">
            <div class="card">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h2>Các loại công việc trong dự án</h2>
                  <p class="text-sm text-slate-500">Được lưu trữ động ở backend và database. Bạn có thể tự do thêm, sửa tên, màu sắc, biểu tượng Material Symbols.</p>
                </div>
                <button type="button" (click)="openAddIssueTypeModal()" class="btn btn-primary flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[18px]">add</span>
                  <span>Thêm loại công việc</span>
                </button>
              </div>

              <div class="divide-y divide-slate-100 dark:divide-slate-800">
                @for (t of issueTypes(); track t.id) {
                  <div class="py-3 flex items-center justify-between gap-4">
                    <div class="flex items-center gap-3">
                      <div class="w-9 h-9 rounded-lg flex items-center justify-center text-white shrink-0 shadow-2xs" [style.backgroundColor]="t.color">
                        <span class="material-symbols-outlined text-[20px]">{{ t.icon }}</span>
                      </div>
                      <div>
                        <div class="flex items-center gap-2">
                          <span class="font-bold text-slate-800 dark:text-slate-200 text-sm">{{ t.name }}</span>
                          @if (t.isSubtask) {
                            <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-amber-50 text-amber-700 border border-amber-200">Subtask</span>
                          }
                          @if (t.isCustom) {
                            <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-blue-50 text-blue-700 border border-blue-200">Tùy biến</span>
                          }
                        </div>
                        <p class="text-xs text-slate-400 mt-0.5">Icon: {{ t.icon }} | Màu: {{ t.color }} | Cấp bậc: {{ t.hierarchyLevel ?? 2 }}</p>
                      </div>
                    </div>
                    <div class="flex items-center gap-1">
                      <button type="button" (click)="openEditIssueTypeModal(t)" class="p-1.5 rounded-md text-slate-500 hover:text-slate-800 hover:bg-slate-100 transition-colors" title="Chỉnh sửa">
                        <span class="material-symbols-outlined text-[18px]">edit</span>
                      </button>
                      <button type="button" (click)="deleteIssueType(t)" class="p-1.5 rounded-md text-red-500 hover:text-red-700 hover:bg-red-50 transition-colors" title="Xóa loại công việc">
                        <span class="material-symbols-outlined text-[18px]">delete</span>
                      </button>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>
        }

        <!-- TAB 3: QUẢN LÝ NHÃN DÁN (LABELS) -->
        @if (canManageProject() && activeTab() === 'labels') {
          <div class="settings-content">
            <div class="card">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h2>Danh sách nhãn dán (Labels)</h2>
                  <p class="text-sm text-slate-500">Gán vào User Story, Task, Bug để phân loại module (frontend, backend, auth, v.v.). Lưu trữ động tại backend.</p>
                </div>
                <button type="button" (click)="openAddLabelModal()" class="btn btn-primary flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[18px]">add</span>
                  <span>Thêm nhãn mới</span>
                </button>
              </div>

              @if (labels().length === 0) {
                <div class="py-8 text-center text-slate-400">
                  <span class="material-symbols-outlined text-[40px] opacity-40 mb-1">label_off</span>
                  <p class="text-sm">Chưa có nhãn dán nào. Hãy thêm nhãn để phân loại công việc thuận tiện hơn.</p>
                </div>
              } @else {
                <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">
                  @for (l of labels(); track l.id) {
                    <div class="p-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50 flex items-center justify-between gap-2 shadow-2xs">
                      <div class="flex items-center gap-2 min-w-0">
                        <span class="w-3.5 h-3.5 rounded-full shrink-0" [style.backgroundColor]="l.colorHex"></span>
                        <span class="font-semibold text-sm text-slate-800 dark:text-slate-200 truncate">{{ l.name }}</span>
                      </div>
                      <div class="flex items-center gap-1 shrink-0">
                        <button type="button" (click)="openEditLabelModal(l)" class="p-1 rounded text-slate-400 hover:text-slate-700" title="Sửa nhãn">
                          <span class="material-symbols-outlined text-[16px]">edit</span>
                        </button>
                        <button type="button" (click)="deleteLabel(l)" class="p-1 rounded text-red-400 hover:text-red-700" title="Xóa nhãn">
                          <span class="material-symbols-outlined text-[16px]">delete</span>
                        </button>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          </div>
        }

        <!-- TAB: QUẢN LÝ MỨC ĐỘ ƯU TIÊN ĐỘNG (PRIORITIES) TỪ DATABASE -->
        @if (canManageProject() && activeTab() === 'priorities') {
          <div class="settings-content space-y-6">
            <!-- 1. CHỌN VÀ ÁP DỤNG MẪU MỨC ĐỘ ƯU TIÊN HỆ THỐNG / DATABASE -->
            <div class="card">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 mb-3">
                <div>
                  <h2 class="text-base font-bold text-slate-900 dark:text-white">Áp dụng mẫu mức độ ưu tiên chuẩn (Từ Cơ sở dữ liệu)</h2>
                  <p class="text-sm text-slate-500">
                    Áp dụng danh sách các mức độ ưu tiên theo chuẩn ngành (Scrum 5 cấp, Agile 4 cấp, Tối giản 3 cấp, Hỗ trợ sự cố 4 cấp) cho riêng dự án này.
                  </p>
                </div>
              </div>

              <div class="flex flex-col sm:flex-row gap-3 items-stretch sm:items-center">
                <select id="priorityTemplateSelect" class="form-control sm:max-w-md" [ngModel]="selectedPriorityTemplateId()" (ngModelChange)="onPriorityTemplateChange($event)">
                  @for (tpl of priorityTemplates(); track tpl.id) {
                    <option [value]="tpl.id">{{ tpl.name }} • [Mẫu hệ thống]</option>
                  }
                </select>
                <button type="button" class="btn btn-primary flex items-center justify-center gap-1.5" [disabled]="submittingPriority() || !canManageProject()" (click)="applySelectedPriorityTemplate()">
                  <span class="material-symbols-outlined text-[18px]">auto_fix_high</span>
                  <span>{{ submittingPriority() ? 'Đang lưu...' : 'Áp dụng mẫu này' }}</span>
                </button>
              </div>

              <!-- Xem trước trực quan các mức độ trong mẫu đã chọn -->
              @if (selectedPriorityTemplateData(); as tpl) {
                <div class="mt-4 pt-4 border-t border-slate-200/80 dark:border-slate-800">
                  <div class="flex items-center justify-between gap-2 mb-2.5">
                    <div class="flex items-center gap-2">
                      <span class="material-symbols-outlined text-[18px] text-primary">visibility</span>
                      <span class="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                        Xem trước các mức độ trong mẫu: <span class="text-primary normal-case font-bold">{{ tpl.name }}</span>
                      </span>
                    </div>
                    <span class="text-[11px] px-2 py-0.5 rounded-full font-medium bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                      {{ tpl.items.length }} mức độ ưu tiên
                    </span>
                  </div>

                  <div class="flex items-center gap-2 flex-wrap p-3 rounded-xl bg-slate-50/80 dark:bg-slate-900/60 border border-slate-200/70 dark:border-slate-800">
                    @for (it of tpl.items; track it.name; let last = $last) {
                      <div class="flex items-center gap-2">
                        <div class="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 shadow-2xs">
                          <div class="w-6 h-6 rounded flex items-center justify-center text-white shrink-0 shadow-2xs" [style.backgroundColor]="it.colorHex">
                            <span class="material-symbols-outlined text-[15px]">{{ it.iconKey || getPriorityIcon(it.level) }}</span>
                          </div>
                          <span class="text-xs font-bold text-slate-800 dark:text-slate-200">{{ it.name }}</span>
                          <span class="text-[10px] px-1.5 py-0.5 rounded font-medium bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300">
                            Cấp {{ it.level }}
                          </span>
                        </div>
                        @if (!last) {
                          <span class="material-symbols-outlined text-[16px] text-slate-400 shrink-0">arrow_forward</span>
                        }
                      </div>
                    }
                  </div>
                </div>
              }
            </div>

            <!-- 2. DANH SÁCH MỨC ĐỘ ƯU TIÊN HIỆN HÀNH CỦA DỰ ÁN & TÙY BIẾN -->
            <div class="card">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-4">
                <div>
                  <h2 class="text-base font-bold text-slate-900 dark:text-white">Mức độ ưu tiên của dự án ({{ projectPriorities().length }} mức độ)</h2>
                  <p class="text-sm text-slate-500">
                    Các mức độ này được áp dụng trực tiếp cho Backlog, Scrum Board và chi tiết công việc trong dự án này.
                  </p>
                </div>
                <div class="flex items-center gap-2 flex-wrap">
                  @if (isProjectCustomPriorities()) {
                    <span class="px-2.5 py-1 rounded-full text-xs font-medium bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300 border border-blue-200 dark:border-blue-800 flex items-center gap-1.5 shrink-0">
                      <span class="w-2 h-2 rounded-full bg-blue-500"></span>
                      Tùy biến riêng của dự án
                    </span>
                    <button type="button" (click)="resetPrioritiesToDefault()" [disabled]="submittingPriority()" class="btn btn-sm btn-secondary flex items-center gap-1 text-slate-600 dark:text-slate-300 hover:text-slate-900 cursor-pointer" title="Khôi phục về mẫu chuẩn hệ thống">
                      <span class="material-symbols-outlined text-[16px]">restart_alt</span>
                      <span>Khôi phục mặc định</span>
                    </button>
                  } @else {
                    <span class="px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800 flex items-center gap-1.5 shrink-0">
                      <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
                      Mặc định hệ thống
                    </span>
                  }
                  <button type="button" (click)="openAddPriorityModal()" class="btn btn-primary flex items-center gap-1.5 shrink-0">
                    <span class="material-symbols-outlined text-[18px]">add</span>
                    <span>Thêm mức ưu tiên</span>
                  </button>
                </div>
              </div>

              <div class="divide-y divide-slate-100 dark:divide-slate-800">
                @for (p of projectPriorities(); track p.id; let idx = $index) {
                  <div class="py-3.5 flex items-center justify-between gap-4">
                    <div class="flex items-center gap-3 min-w-0">
                      <div class="w-9 h-9 rounded-lg flex items-center justify-center text-white shrink-0 shadow-2xs" [style.backgroundColor]="p.colorHex">
                        <span class="material-symbols-outlined text-[20px]">{{ p.iconKey || getPriorityIcon(p.level) }}</span>
                      </div>
                      <div class="min-w-0">
                        <div class="flex items-center gap-2 flex-wrap">
                          <span class="font-bold text-slate-800 dark:text-slate-200 text-sm">{{ p.name }}</span>
                          <span class="px-2 py-0.5 rounded-full text-[11px] font-medium border"
                            [class.bg-rose-50]="p.level <= 2" [class.text-rose-700]="p.level <= 2" [class.border-rose-200]="p.level <= 2"
                            [class.bg-amber-50]="p.level === 3" [class.text-amber-700]="p.level === 3" [class.border-amber-200]="p.level === 3"
                            [class.bg-emerald-50]="p.level >= 4" [class.text-emerald-700]="p.level >= 4" [class.border-emerald-200]="p.level >= 4">
                            Cấp độ {{ p.level }}
                          </span>
                          @if (!p.projectId) {
                            <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                              Mặc định hệ thống
                            </span>
                          } @else {
                            <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-blue-50 text-blue-600 dark:bg-blue-950/40 dark:text-blue-300 border border-blue-200 dark:border-blue-800">
                              Tùy biến dự án
                            </span>
                          }
                        </div>
                        <p class="text-xs text-slate-400 mt-0.5">Mã màu: {{ p.colorHex }} | Biểu tượng: {{ p.iconKey || getPriorityIcon(p.level) }}</p>
                      </div>
                    </div>

                    <div class="flex items-center gap-1 shrink-0">
                      <button type="button" (click)="movePriority(idx, -1)" [disabled]="idx === 0 || submittingPriority()" class="p-1.5 rounded-md text-slate-400 hover:text-slate-700 disabled:opacity-20 transition-colors" title="Tăng cấp độ ưu tiên (lên trên)">
                        <span class="material-symbols-outlined text-[18px]">arrow_upward</span>
                      </button>
                      <button type="button" (click)="movePriority(idx, 1)" [disabled]="idx === projectPriorities().length - 1 || submittingPriority()" class="p-1.5 rounded-md text-slate-400 hover:text-slate-700 disabled:opacity-20 transition-colors" title="Giảm cấp độ ưu tiên (xuống dưới)">
                        <span class="material-symbols-outlined text-[18px]">arrow_downward</span>
                      </button>
                      <button type="button" (click)="openEditPriorityModal(p)" class="p-1.5 rounded-md text-slate-500 hover:text-slate-800 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors" title="Chỉnh sửa mức ưu tiên">
                        <span class="material-symbols-outlined text-[18px]">edit</span>
                      </button>
                      <button type="button" (click)="deletePriority(p)" class="p-1.5 rounded-md text-red-500 hover:text-red-700 hover:bg-red-50 dark:hover:bg-red-950/40 transition-colors" title="Xóa mức ưu tiên">
                        <span class="material-symbols-outlined text-[18px]">delete</span>
                      </button>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>
        }

        <!-- TAB 4: CẤU HÌNH WORKFLOW -->
        @if (canManageProject() && activeTab() === 'workflow') {
          <div class="settings-content">
            <app-workflow-settings-page [embedded]="true" [projectIdInput]="projectId"></app-workflow-settings-page>
          </div>
        }

        <!-- TAB 5: CẤU HÌNH BOARD -->
        @if (canManageProject() && activeTab() === 'board') {
          <div class="settings-content">
            <app-board-settings-page [embedded]="true" [projectIdInput]="projectId"></app-board-settings-page>
          </div>
        }

        <!-- TAB 6: THÀNH VIÊN & PHÂN QUYỀN -->
        @if (activeTab() === 'members') {
          <div class="settings-content">
            <app-project-members-page [embedded]="true"></app-project-members-page>
          </div>
        }
      }

      <!-- MODAL TẠO / SỬA ISSUE TYPE -->
      @if (showIssueTypeModal()) {
        <div class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div class="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl max-w-md w-full p-6 animate-in fade-in zoom-in-95 duration-150">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <span class="material-symbols-outlined text-primary">{{ editingIssueType() ? 'edit' : 'add_circle' }}</span>
                <span>{{ editingIssueType() ? 'Sửa Loại Công Việc' : 'Thêm Loại Công Việc Mới' }}</span>
              </h3>
              <button type="button" (click)="showIssueTypeModal.set(false)" class="text-slate-400 hover:text-slate-600">
                <span class="material-symbols-outlined">close</span>
              </button>
            </div>

            <div class="space-y-4">
              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Tên loại công việc <span class="text-red-500">*</span></label>
                <input type="text" [(ngModel)]="issueTypeForm.name" placeholder="Ví dụ: Spike, Research, Release..." class="form-control" />
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Biểu tượng Material Symbol</label>
                <div class="flex items-center gap-2 mb-2">
                  <div class="w-9 h-9 rounded-lg flex items-center justify-center text-white shrink-0 shadow-2xs" [style.backgroundColor]="issueTypeForm.color">
                    <span class="material-symbols-outlined text-[20px]">{{ issueTypeForm.icon }}</span>
                  </div>
                  <input type="text" [(ngModel)]="issueTypeForm.icon" placeholder="Tên icon, ví dụ: bolt, task, biotech..." class="form-control font-mono text-xs" />
                </div>
                <div class="flex flex-wrap gap-1.5 pt-1">
                  @for (ic of sampleIcons; track ic) {
                    <button type="button" (click)="issueTypeForm.icon = ic" class="p-1.5 rounded-lg border border-slate-200 hover:border-primary hover:bg-primary/5 transition-colors" [class.border-primary]="issueTypeForm.icon === ic">
                      <span class="material-symbols-outlined text-[18px] text-slate-700 dark:text-slate-300">{{ ic }}</span>
                    </button>
                  }
                </div>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Mã màu (Hex)</label>
                <div class="flex items-center gap-2 mb-2">
                  <input type="color" [(ngModel)]="issueTypeForm.color" class="w-9 h-9 rounded-lg border border-slate-300 cursor-pointer p-0.5" />
                  <input type="text" [(ngModel)]="issueTypeForm.color" class="form-control font-mono text-xs" />
                </div>
                <div class="flex flex-wrap gap-2 pt-1">
                  @for (col of sampleColors; track col) {
                    <button type="button" (click)="issueTypeForm.color = col" [attr.aria-label]="'Chọn màu ' + col" class="w-6 h-6 rounded-full border-2 border-white shadow-2xs hover:scale-110 transition-transform" [style.backgroundColor]="col">
                      <span class="sr-only">Màu {{ col }}</span>
                    </button>
                  }
                </div>
              </div>

              <div class="pt-2">
                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="issueTypeForm.isSubtask" class="w-4 h-4 rounded text-primary focus:ring-primary" />
                  <span class="text-sm font-medium text-slate-700 dark:text-slate-300">Là công việc con (Subtask / công việc cấp dưới)?</span>
                </label>
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-2">
              <button type="button" (click)="showIssueTypeModal.set(false)" class="btn btn-secondary">Hủy bỏ</button>
              <button type="button" (click)="saveIssueType()" [disabled]="!issueTypeForm.name.trim()" class="btn btn-primary">Lưu loại công việc</button>
            </div>
          </div>
        </div>
      }

      <!-- MODAL TẠO / SỬA NHÃN (LABEL) -->
      @if (showLabelModal()) {
        <div class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div class="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl max-w-sm w-full p-6 animate-in fade-in zoom-in-95 duration-150">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <span class="material-symbols-outlined text-primary">{{ editingLabel() ? 'edit' : 'new_label' }}</span>
                <span>{{ editingLabel() ? 'Sửa Nhãn Dán' : 'Thêm Nhãn Mới' }}</span>
              </h3>
              <button type="button" (click)="showLabelModal.set(false)" class="text-slate-400 hover:text-slate-600">
                <span class="material-symbols-outlined">close</span>
              </button>
            </div>

            <div class="space-y-4">
              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Tên nhãn <span class="text-red-500">*</span></label>
                <input type="text" [(ngModel)]="labelForm.name" placeholder="frontend, backend, api, security..." class="form-control" />
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Mã màu (Hex)</label>
                <div class="flex items-center gap-2 mb-2">
                  <input type="color" [(ngModel)]="labelForm.colorHex" class="w-9 h-9 rounded-lg border border-slate-300 cursor-pointer p-0.5" />
                  <input type="text" [(ngModel)]="labelForm.colorHex" class="form-control font-mono text-xs" />
                </div>
                <div class="flex flex-wrap gap-2 pt-1">
                  @for (col of sampleColors; track col) {
                    <button type="button" (click)="labelForm.colorHex = col" [attr.aria-label]="'Chọn màu ' + col" class="w-6 h-6 rounded-full border-2 border-white shadow-2xs hover:scale-110 transition-transform" [style.backgroundColor]="col">
                      <span class="sr-only">Màu {{ col }}</span>
                    </button>
                  }
                </div>
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-2">
              <button type="button" (click)="showLabelModal.set(false)" class="btn btn-secondary">Hủy bỏ</button>
              <button type="button" (click)="saveLabel()" [disabled]="!labelForm.name.trim()" class="btn btn-primary">Lưu nhãn</button>
            </div>
          </div>
        </div>
      }

      <!-- MODAL TẠO / SỬA MỨC ĐỘ ƯU TIÊN (PRIORITY) -->
      @if (showPriorityModal()) {
        <div class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div class="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl max-w-md w-full p-6 animate-in fade-in zoom-in-95 duration-150">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <span class="material-symbols-outlined text-primary">{{ editingPriority() ? 'edit' : 'flag' }}</span>
                <span>{{ editingPriority() ? 'Sửa Mức Độ Ưu Tiên' : 'Thêm Mức Độ Ưu Tiên Mới' }}</span>
              </h3>
              <button type="button" (click)="showPriorityModal.set(false)" class="text-slate-400 hover:text-slate-600">
                <span class="material-symbols-outlined">close</span>
              </button>
            </div>

            <div class="space-y-4">
              <!-- Live Preview -->
              <div class="p-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/60 flex items-center justify-between">
                <div class="flex items-center gap-2.5">
                  <div class="w-8 h-8 rounded-lg flex items-center justify-center text-white shadow-2xs" [style.backgroundColor]="priorityForm.colorHex">
                    <span class="material-symbols-outlined text-[18px]">{{ priorityForm.iconKey || getPriorityIcon(priorityForm.level) }}</span>
                  </div>
                  <div>
                    <span class="font-bold text-xs text-slate-800 dark:text-slate-100">{{ priorityForm.name || 'Tên mức độ' }}</span>
                    <span class="ml-2 text-[10px] px-1.5 py-0.5 rounded font-medium bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300">
                      Cấp độ {{ priorityForm.level }}
                    </span>
                  </div>
                </div>
                <span class="text-[11px] font-mono text-slate-400">{{ priorityForm.colorHex }}</span>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Tên mức độ <span class="text-red-500">*</span></label>
                <input type="text" [(ngModel)]="priorityForm.name" placeholder="Highest, Urgent, High, Critical, Bình thường..." class="form-control" />
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Cấp độ ưu tiên (Level)</label>
                <input type="number" min="1" max="10" [(ngModel)]="priorityForm.level" class="form-control" />
                <small class="text-[11px] text-slate-400 mt-1 block">Cấp 1 là mức ưu tiên cao nhất / khẩn cấp nhất, cấp tăng dần mức độ giảm dần.</small>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Mã màu (Hex)</label>
                <div class="flex items-center gap-2 mb-2">
                  <input type="color" [(ngModel)]="priorityForm.colorHex" class="w-9 h-9 rounded-lg border border-slate-300 cursor-pointer p-0.5" />
                  <input type="text" [(ngModel)]="priorityForm.colorHex" class="form-control font-mono text-xs" />
                </div>
                <div class="flex flex-wrap gap-2 pt-1">
                  @for (col of samplePriorityColors; track col) {
                    <button type="button" (click)="priorityForm.colorHex = col" [attr.aria-label]="'Chọn màu ' + col" class="w-6 h-6 rounded-full border-2 border-white shadow-2xs hover:scale-110 transition-transform" [style.backgroundColor]="col">
                      <span class="sr-only">Màu {{ col }}</span>
                    </button>
                  }
                </div>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Biểu tượng Material Symbols</label>
                <input type="text" [(ngModel)]="priorityForm.iconKey" placeholder="keyboard_double_arrow_up, arrow_upward, flag, priority_high..." class="form-control font-mono text-xs mb-2" />
                <div class="flex flex-wrap gap-1.5 pt-1">
                  @for (ico of samplePriorityIcons; track ico) {
                    <button type="button" (click)="priorityForm.iconKey = ico" [class.bg-primary]="priorityForm.iconKey === ico" [class.text-white]="priorityForm.iconKey === ico" class="p-1.5 rounded-lg border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors" [title]="ico">
                      <span class="material-symbols-outlined text-[18px]">{{ ico }}</span>
                    </button>
                  }
                </div>
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-2">
              <button type="button" (click)="showPriorityModal.set(false)" class="btn btn-secondary">Hủy bỏ</button>
              <button type="button" (click)="savePriority()" [disabled]="!priorityForm.name.trim() || submittingPriority()" class="btn btn-primary">
                {{ submittingPriority() ? 'Đang lưu...' : 'Lưu mức ưu tiên' }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .settings-container {
      padding: 1.75rem 2rem;
      max-width: 1180px;
      width: 100%;
      box-sizing: border-box;
      margin: 0 auto;
    }
    .breadcrumb {
      font-size: 0.85rem;
      color: #64748b;
      margin-bottom: 0.75rem;
    }
    .breadcrumb a {
      color: #1b4332;
      text-decoration: none;
    }
    .settings-header {
      margin-bottom: 1.25rem;
    }
    h1 {
      font-size: 1.6rem;
      font-weight: 700;
      color: #1e293b;
      margin: 0;
    }
    .subtitle {
      color: #64748b;
      font-size: 0.9rem;
      margin-top: 0.25rem;
    }
    .settings-tabs {
      display: flex;
      align-items: center;
      gap: 0.25rem;
      border-bottom: 2px solid #e2e8f0;
      margin: 1.5rem 0 1.75rem 0;
      overflow-x: auto;
      scrollbar-width: none;
    }
    .settings-tabs::-webkit-scrollbar {
      display: none;
    }
    .tab-item {
      padding: 0.75rem 1rem;
      font-weight: 600;
      font-size: 0.9rem;
      color: #64748b;
      background: transparent;
      border: none;
      text-decoration: none;
      border-bottom: 2px solid transparent;
      margin-bottom: -2px;
      cursor: pointer;
      transition: color 0.15s, border-color 0.15s;
      white-space: nowrap;
      flex: 0 0 auto;
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
    }
    .tab-item:hover {
      color: #1b4332;
    }
    .tab-item.active {
      color: #1b4332;
      border-bottom-color: #2d6a4f;
    }
    .card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.75rem;
      margin-bottom: 1.5rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    h2 {
      font-size: 1.2rem;
      font-weight: 600;
      color: #0f172a;
      margin: 0 0 0.5rem 0;
    }
    .form-group {
      margin-bottom: 1.25rem;
    }
    label {
      display: block;
      font-weight: 600;
      color: #334155;
      font-size: 0.9rem;
      margin-bottom: 0.35rem;
    }
    .required {
      color: #ef4444;
    }
    .form-control {
      width: 100%;
      padding: 0.65rem 0.85rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      font-size: 0.95rem;
      box-sizing: border-box;
    }
    .form-control:focus {
      outline: none;
      border-color: #2d6a4f;
      box-shadow: 0 0 0 3px rgba(45, 106, 79, 0.15);
    }
    .readonly-input {
      background: #f8fafc;
      color: #64748b;
      cursor: not-allowed;
      font-family: monospace;
      font-weight: bold;
    }
    .form-hint {
      display: block;
      color: #64748b;
      font-size: 0.8rem;
      margin-top: 0.35rem;
    }
    .form-actions {
      margin-top: 1.5rem;
      display: flex;
      justify-content: flex-end;
    }
    .btn {
      padding: 0.65rem 1.4rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.9rem;
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 0.2s;
    }
    .btn-primary {
      background: #1b4332;
      color: #ffffff;
    }
    .btn-primary:hover:not(:disabled) {
      background: #2d6a4f;
    }
    .btn-secondary {
      background: #f1f5f9;
      color: #475569;
      border-color: #cbd5e1;
    }
    .btn-secondary:hover {
      background: #e2e8f0;
      color: #1e293b;
    }
    .btn-danger {
      background: #dc2626;
      color: #ffffff;
    }
    .btn-danger:hover {
      background: #b91c1c;
    }
    .danger-card {
      border-color: #fecaca;
      background: #fffafa;
    }
    .danger-card h3 {
      color: #991b1b;
      margin: 0 0 0.5rem 0;
    }
    .danger-card p {
      color: #7f1d1d;
      font-size: 0.9rem;
      margin-bottom: 1rem;
    }
    .alert {
      padding: 0.85rem 1rem;
      border-radius: 8px;
      margin-bottom: 1.25rem;
      font-size: 0.9rem;
    }
    .alert-error {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #991b1b;
    }
    .loading-box {
      text-align: center;
      padding: 3rem;
    }
    .spinner {
      width: 36px;
      height: 36px;
      border: 3px solid #e2e8f0;
      border-top-color: #2563eb;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 1rem auto;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectSettingsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly projectService = inject(ProjectManagementService);
  private readonly confirmService = inject(ConfirmDialogService);
  private readonly toastService = inject(ToastService);
  private readonly workspaceData = inject(WorkspaceDataSource, { optional: true });
  private readonly workspaceContext = inject(WorkspaceContextService, { optional: true });
  private readonly identity = inject(IdentityService);

  readonly canManageProject = computed(() => {
    if (this.identity.isSystemAdmin()) return true;
    const currentUser = this.identity.authState().currentUser;
    const myId = currentUser?.id;
    const myName = currentUser?.displayName;
    const myEmail = currentUser?.email;
    if (!myId) return false;
    const org = this.workspaceContext?.currentOrg();
    if (org && (org.myRole === 'Owner' || (org.ownerId && org.ownerId.toLowerCase() === myId.toLowerCase()))) {
      return true;
    }
    const proj = this.project() || this.projectService.currentProject();
    if (proj) {
      if (proj.leadUserId && proj.leadUserId.toLowerCase() === myId.toLowerCase()) return true;
      if (proj.leadName && myName && proj.leadName.toLowerCase() === myName.toLowerCase()) return true;
      if (proj.members) {
        const m = proj.members.find(x =>
          (x.userId && x.userId.toLowerCase() === myId.toLowerCase()) ||
          (x.email && myEmail && x.email.toLowerCase() === myEmail.toLowerCase()) ||
          (x.displayName && myName && x.displayName.toLowerCase() === myName.toLowerCase())
        );
        if (m) {
          const r = m.role.toLowerCase();
          if (r.includes('lead') || r.includes('manager') || r.includes('scrum') || r.includes('director') || r.includes('admin') || r.includes('owner')) {
            return true;
          }
        }
      }
    }
    return false;
  });

  projectId = '';
  readonly activeTab = signal<ProjectSettingsTab>('general');
  readonly project = signal<Project | null>(null);
  readonly users = signal<UserDisplayInfo[]>([]);
  readonly issueTypes = signal<CustomIssueType[]>([]);
  readonly labels = signal<ProjectLabel[]>([]);
  readonly projectPriorities = this.projectService.projectPriorities;

  // Cấu hình mức độ ưu tiên động (Dynamic Priorities & Templates)
  readonly priorityTemplates = signal<PriorityTemplate[]>([]);
  readonly selectedPriorityTemplateId = signal<string>('scrum-std-5');
  readonly selectedPriorityTemplateData = computed(() => {
    const tpls = this.priorityTemplates();
    const selId = this.selectedPriorityTemplateId();
    return tpls.find(t => t.id === selId) || tpls[0] || null;
  });
  readonly submittingPriority = signal<boolean>(false);
  readonly isProjectCustomPriorities = computed(() => {
    return this.projectPriorities().some(p => !!p.projectId || p.isCustom);
  });
  readonly showPriorityModal = signal<boolean>(false);
  readonly editingPriority = signal<ProjectPriority | null>(null);

  priorityForm = {
    name: '',
    level: 3,
    colorHex: '#E97F33',
    iconKey: 'drag_handle'
  };

  samplePriorityColors = [
    '#CD1317', '#E53E3E', '#E9494B', '#DD6B20', '#E97F33',
    '#D69E2E', '#319795', '#2A8735', '#38A169', '#57A55A',
    '#3182CE', '#6366F1', '#805AD5', '#718096'
  ];

  samplePriorityIcons = [
    'keyboard_double_arrow_up', 'keyboard_arrow_up', 'drag_handle',
    'keyboard_arrow_down', 'keyboard_double_arrow_down', 'flag',
    'priority_high', 'bolt', 'warning', 'notification_important',
    'emergency', 'hourglass_top'
  ];

  getPriorityIcon(level: number): string {
    switch (level) {
      case 1: return 'keyboard_double_arrow_up';
      case 2: return 'keyboard_arrow_up';
      case 3: return 'drag_handle';
      case 4: return 'keyboard_arrow_down';
      case 5: return 'keyboard_double_arrow_down';
      default: return 'flag';
    }
  }
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  // Modal states
  readonly showIssueTypeModal = signal<boolean>(false);
  readonly editingIssueType = signal<CustomIssueType | null>(null);
  issueTypeForm = {
    name: '',
    icon: 'task_alt',
    color: '#6366F1',
    isSubtask: false
  };

  readonly showLabelModal = signal<boolean>(false);
  readonly editingLabel = signal<ProjectLabel | null>(null);
  labelForm = {
    name: '',
    colorHex: '#3B82F6'
  };

  sampleIcons = ['bookmark', 'task_alt', 'bug_report', 'bolt', 'subdirectory_arrow_right', 'biotech', 'palette', 'security', 'description', 'flag', 'code', 'build'];
  sampleColors = ['#EF4444', '#F97316', '#F59E0B', '#10B981', '#06B6D4', '#3B82F6', '#6366F1', '#8B5CF6', '#EC4899', '#64748B'];

  editForm: UpdateProjectRequest = {
    name: '',
    description: '',
    leadUserId: ''
  };

  constructor() {
    effect(() => {
      const current = this.projectService.currentProject();
      if (current?.id && !this.projectId) {
        this.projectId = current.id;
        this.applyCachedProject(current);
        this.loadProject();
        this.loadUsers();
        this.loadIssueTypes();
        this.loadLabels();
        this.loadPriorityTemplates();
      }
    });
  }

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id') || this.projectService.currentProject().id || '';
    const cached = this.projectService.currentProject();
    if (cached && (!this.projectId || cached.id === this.projectId)) {
      this.projectId = this.projectId || cached.id;
      this.applyCachedProject(cached);
    }

    const routeDataTab = this.route.snapshot.data['tab'] as ProjectSettingsTab | undefined;
    const urlPath = this.router.url;
    if (routeDataTab) {
      this.activeTab.set(!this.canManageProject() && routeDataTab !== 'members' ? 'members' : routeDataTab);
    } else if (urlPath.includes('/workflow') || urlPath.includes('/board')) {
      this.activeTab.set(this.canManageProject() ? 'workflow' : 'members');
    } else if (urlPath.includes('/members')) {
      this.activeTab.set('members');
    } else if (!this.canManageProject()) {
      this.activeTab.set('members');
    }

    this.route.queryParamMap.subscribe(q => {
      const tab = q.get('tab') as ProjectSettingsTab | null;
      if (tab === 'issue-types' || tab === 'labels' || tab === 'priorities' || tab === 'general' || tab === 'workflow' || tab === 'members') {
        this.activeTab.set(!this.canManageProject() && tab !== 'members' ? 'members' : tab);
      } else if (tab === 'board') {
        this.activeTab.set(this.canManageProject() ? 'workflow' : 'members');
      } else if (!this.route.snapshot.data['tab'] && !this.router.url.includes('/workflow') && !this.router.url.includes('/board') && !this.router.url.includes('/members')) {
        this.activeTab.set(this.canManageProject() ? 'general' : 'members');
      }
    });

    if (this.projectId) {
      this.loadProject();
      this.loadUsers();
      this.loadIssueTypes();
      this.loadLabels();
      this.loadPriorityTemplates();
    }
  }

  private applyCachedProject(p: Project): void {
    this.project.set(p);
    this.editForm = {
      name: p.name,
      description: p.description || '',
      leadUserId: p.leadUserId
    };
    const cachedTypes = this.projectService.customIssueTypes();
    if (cachedTypes.length > 0 && this.issueTypes().length === 0) {
      this.issueTypes.set(cachedTypes);
    }
  }

  selectTab(tab: ProjectSettingsTab): void {
    if (!this.canManageProject() && tab !== 'members') {
      this.toastService.warning('Phân quyền', 'Chỉ Quản lý dự án trở lên mới có quyền truy cập cài đặt cấu hình.');
      return;
    }
    this.activeTab.set(tab);
    if (this.projectId) {
      void this.router.navigate(['/projects', this.projectId, 'settings'], {
        queryParams: { tab: tab === 'general' ? null : tab },
        replaceUrl: true
      });
    }
  }

  loadUsers(): void {
    const orgId = this.project()?.orgId;
    if (orgId && this.workspaceData) {
      this.workspaceData.listMembers(orgId).subscribe({
        next: orgMembers => {
          if (orgMembers && orgMembers.length > 0) {
            this.users.set(orgMembers.map(m => ({
              userId: m.userId,
              displayName: m.displayName || m.email,
              avatarUrl: m.avatarUrl || undefined
            })));
            return;
          }
          this.fallbackLoadUsers();
        },
        error: () => this.fallbackLoadUsers()
      });
    } else {
      this.fallbackLoadUsers();
    }
  }

  private fallbackLoadUsers(): void {
    this.projectService.getUsers().subscribe({
      next: users => this.users.set(users),
      error: () => undefined
    });
  }

  loadProject(): void {
    if (!this.project()) {
      this.loading.set(true);
    }
    this.error.set(null);
    this.projectService.getProjectById(this.projectId).subscribe({
      next: (p) => {
        this.project.set(p);
        this.editForm = {
          name: p.name,
          description: p.description || '',
          leadUserId: p.leadUserId
        };
        this.loading.set(false);
        this.loadUsers();
      },
      error: (err) => {
        if (!this.project()) {
          this.error.set(apiErrorMessage(err, 'Không tìm thấy dự án.'));
        }
        this.loading.set(false);
      }
    });
  }

  loadIssueTypes(): void {
    this.projectService.getIssueTypes(this.projectId).subscribe({
      next: types => this.issueTypes.set(types),
      error: err => console.error('Không thể tải loại công việc:', err)
    });
  }

  loadLabels(): void {
    this.projectService.getProjectLabels(this.projectId).subscribe({
      next: labels => this.labels.set(labels),
      error: err => console.error('Không thể tải nhãn dán:', err)
    });
  }

  onSaveGeneral(): void {
    if (this.saving() || this.loading() || !this.project()) return;
    this.saving.set(true);

    this.projectService.updateProject(this.projectId, this.editForm).subscribe({
      next: (updated) => {
        this.project.set(updated);
        this.saving.set(false);
        this.toastService.success('Cập Nhật Dự Án', 'Đã lưu thay đổi cấu hình dự án thành công.');
      },
      error: (err) => {
        this.saving.set(false);
        this.toastService.error('Lỗi Cập Nhật', apiErrorMessage(err, 'Không thể lưu thay đổi.'));
      }
    });
  }

  onDeleteProject(): void {
    const projName = this.project()?.name || 'dự án này';
    this.confirmService.confirm({
      title: 'Xóa Dự Án',
      message: `Bạn có chắc chắn muốn xóa dự án "${projName}" không? Hành động này sẽ lưu trữ dự án.`,
      type: 'danger',
      confirmText: 'Xóa dự án ngay',
      cancelText: 'Hủy bỏ',
      onConfirm: () => {
        this.projectService.deleteProject(this.projectId).subscribe({
          next: () => {
            this.toastService.warning('Đã Xóa Dự Án', `Đã chuyển dự án "${projName}" sang trạng thái lưu trữ.`);
            this.router.navigate(['/projects']);
          },
          error: (err) => {
            this.toastService.error('Lỗi Xóa Dự Án', apiErrorMessage(err, 'Không thể xóa dự án.'));
          }
        });
      }
    });
  }

  // Issue Type Actions
  openAddIssueTypeModal(): void {
    this.editingIssueType.set(null);
    this.issueTypeForm = { name: '', icon: 'task_alt', color: '#6366F1', isSubtask: false };
    this.showIssueTypeModal.set(true);
  }

  openEditIssueTypeModal(type: CustomIssueType): void {
    this.editingIssueType.set(type);
    this.issueTypeForm = {
      name: type.name,
      icon: type.icon || 'task_alt',
      color: type.color || '#6366F1',
      isSubtask: !!type.isSubtask
    };
    this.showIssueTypeModal.set(true);
  }

  saveIssueType(): void {
    const name = this.issueTypeForm.name.trim();
    if (!name) return;

    const editing = this.editingIssueType();
    if (editing) {
      this.projectService.updateCustomIssueType(
        this.projectId,
        editing.id,
        name,
        this.issueTypeForm.icon,
        this.issueTypeForm.color,
        this.issueTypeForm.isSubtask
      ).subscribe({
        next: (updated) => {
          this.issueTypes.update(types => types.map(t => t.id === editing.id ? updated : t));
          this.showIssueTypeModal.set(false);
          this.toastService.success('Loại Công Việc', `Đã cập nhật loại công việc "${name}".`);
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể cập nhật loại công việc.'))
      });
    } else {
      this.projectService.addCustomIssueType(
        name,
        this.issueTypeForm.icon,
        this.issueTypeForm.color,
        this.issueTypeForm.isSubtask
      ).subscribe({
        next: (created) => {
          this.issueTypes.update(types => [...types, created]);
          this.showIssueTypeModal.set(false);
          this.toastService.success('Loại Công Việc', `Đã tạo loại công việc "${name}".`);
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể tạo loại công việc.'))
      });
    }
  }

  deleteIssueType(type: CustomIssueType): void {
    this.confirmService.confirm({
      title: 'Xóa Loại Công Việc',
      message: `Bạn có chắc muốn xóa loại "${type.name}" không? Thao tác này sẽ bị từ chối nếu còn công việc thuộc loại này.`,
      type: 'danger',
      confirmText: 'Xóa',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.projectService.deleteCustomIssueType(this.projectId, type.id).subscribe({
          next: () => {
            this.issueTypes.update(types => types.filter(t => t.id !== type.id));
            this.toastService.success('Đã Xóa', `Đã xóa loại công việc "${type.name}".`);
          },
          error: (err) => this.toastService.error('Lỗi Xóa', apiErrorMessage(err, 'Không thể xóa loại công việc.'))
        });
      }
    });
  }

  // Label Actions
  openAddLabelModal(): void {
    this.editingLabel.set(null);
    this.labelForm = { name: '', colorHex: '#3B82F6' };
    this.showLabelModal.set(true);
  }

  openEditLabelModal(label: ProjectLabel): void {
    this.editingLabel.set(label);
    this.labelForm = { name: label.name, colorHex: label.colorHex || '#3B82F6' };
    this.showLabelModal.set(true);
  }

  saveLabel(): void {
    const name = this.labelForm.name.trim();
    if (!name) return;

    const editing = this.editingLabel();
    if (editing) {
      this.projectService.updateProjectLabel(editing.id, name, this.labelForm.colorHex).subscribe({
        next: (updated) => {
          this.labels.update(labels => labels.map(l => l.id === editing.id ? updated : l));
          this.showLabelModal.set(false);
          this.toastService.success('Nhãn Dán', `Đã cập nhật nhãn "${name}".`);
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể cập nhật nhãn.'))
      });
    } else {
      this.projectService.createProjectLabel(this.projectId, name, this.labelForm.colorHex).subscribe({
        next: (created) => {
          this.labels.update(labels => [...labels, created]);
          this.showLabelModal.set(false);
          this.toastService.success('Nhãn Dán', `Đã tạo nhãn "${name}".`);
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể tạo nhãn mới.'))
      });
    }
  }

  deleteLabel(label: ProjectLabel): void {
    this.confirmService.confirm({
      title: 'Xóa Nhãn',
      message: `Bạn có chắc muốn xóa nhãn "${label.name}" không?`,
      type: 'danger',
      confirmText: 'Xóa',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.projectService.deleteProjectLabel(label.id).subscribe({
          next: () => {
            this.labels.update(labels => labels.filter(l => l.id !== label.id));
            this.toastService.success('Đã Xóa', `Đã xóa nhãn "${label.name}".`);
          },
          error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể xóa nhãn.'))
        });
      }
    });
  }

  // ==================== CẤU HÌNH MỨC ĐỘ ƯU TIÊN (PRIORITIES) ====================
  loadPriorityTemplates(): void {
    if (!this.projectId) return;
    this.projectService.getPriorityTemplates(this.projectId).subscribe({
      next: (tpls) => {
        if (tpls && tpls.length > 0) {
          this.priorityTemplates.set(tpls);
          if (!this.selectedPriorityTemplateId() || !tpls.some(t => t.id === this.selectedPriorityTemplateId())) {
            this.selectedPriorityTemplateId.set(tpls[0].id);
          }
        }
      },
      error: () => {}
    });
    this.projectService.loadProjectPriorities(this.projectId).subscribe();
  }

  onPriorityTemplateChange(templateId: string): void {
    this.selectedPriorityTemplateId.set(templateId);
  }

  applySelectedPriorityTemplate(): void {
    if (!this.canManageProject() || !this.projectId) return;
    const templateId = this.selectedPriorityTemplateId();
    if (!templateId) return;

    this.confirmService.confirm({
      title: 'Áp dụng mẫu mức độ ưu tiên',
      message: 'Bạn có chắc chắn muốn áp dụng mẫu này cho dự án? Các mức độ ưu tiên hiện tại của dự án sẽ được thay thế bằng danh sách của mẫu đã chọn.',
      type: 'info',
      confirmText: 'Áp dụng',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.submittingPriority.set(true);
        this.projectService.applyPriorityTemplate(this.projectId, templateId).subscribe({
          next: () => {
            this.submittingPriority.set(false);
            this.toastService.success('Thành công', 'Đã áp dụng mẫu mức độ ưu tiên cho dự án.');
          },
          error: (err) => {
            this.submittingPriority.set(false);
            this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể áp dụng mẫu mức độ ưu tiên.'));
          }
        });
      }
    });
  }

  resetPrioritiesToDefault(): void {
    if (!this.canManageProject() || !this.projectId) return;
    this.confirmService.confirm({
      title: 'Khôi phục mặc định hệ thống',
      message: 'Bạn có chắc muốn xóa tất cả cấu hình riêng và đưa mức độ ưu tiên của dự án về mẫu chuẩn mặc định của hệ thống?',
      type: 'warning',
      confirmText: 'Khôi phục',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.submittingPriority.set(true);
        this.projectService.resetProjectPriorities(this.projectId).subscribe({
          next: () => {
            this.submittingPriority.set(false);
            this.toastService.success('Thành công', 'Đã đưa mức độ ưu tiên về mặc định hệ thống.');
          },
          error: (err) => {
            this.submittingPriority.set(false);
            this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể khôi phục mặc định.'));
          }
        });
      }
    });
  }

  openAddPriorityModal(): void {
    this.editingPriority.set(null);
    const currentCount = this.projectPriorities().length;
    this.priorityForm = {
      name: '',
      level: currentCount + 1,
      colorHex: '#3B82F6',
      iconKey: 'flag'
    };
    this.showPriorityModal.set(true);
  }

  openEditPriorityModal(p: ProjectPriority): void {
    this.editingPriority.set(p);
    this.priorityForm = {
      name: p.name,
      level: p.level,
      colorHex: p.colorHex || '#3B82F6',
      iconKey: p.iconKey || this.getPriorityIcon(p.level)
    };
    this.showPriorityModal.set(true);
  }

  savePriority(): void {
    const name = this.priorityForm.name.trim();
    if (!name || !this.projectId) return;
    const editing = this.editingPriority();
    this.submittingPriority.set(true);

    if (editing) {
      this.projectService.updateProjectPriority(this.projectId, editing.id, {
        name,
        level: this.priorityForm.level,
        colorHex: this.priorityForm.colorHex,
        iconKey: this.priorityForm.iconKey
      }).subscribe({
        next: () => {
          this.submittingPriority.set(false);
          this.showPriorityModal.set(false);
          this.toastService.success('Mức độ ưu tiên', `Đã cập nhật mức ưu tiên "${name}".`);
        },
        error: (err) => {
          this.submittingPriority.set(false);
          this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể cập nhật mức ưu tiên.'));
        }
      });
    } else {
      this.projectService.createProjectPriority(this.projectId, {
        name,
        level: this.priorityForm.level,
        colorHex: this.priorityForm.colorHex,
        iconKey: this.priorityForm.iconKey
      }).subscribe({
        next: () => {
          this.submittingPriority.set(false);
          this.showPriorityModal.set(false);
          this.toastService.success('Mức độ ưu tiên', `Đã thêm mức ưu tiên "${name}".`);
        },
        error: (err) => {
          this.submittingPriority.set(false);
          this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể tạo mức ưu tiên.'));
        }
      });
    }
  }

  deletePriority(p: ProjectPriority): void {
    if (this.projectPriorities().length <= 1) {
      this.toastService.warning('Cảnh báo', 'Dự án phải có ít nhất 1 mức độ ưu tiên.');
      return;
    }
    this.confirmService.confirm({
      title: 'Xóa mức độ ưu tiên',
      message: `Bạn có chắc muốn xóa mức độ ưu tiên "${p.name}"? Các công việc đang gán mức này sẽ được chuyển về mức ưu tiên mặc định.`,
      type: 'danger',
      confirmText: 'Xóa',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.submittingPriority.set(true);
        this.projectService.deleteProjectPriority(this.projectId, p.id).subscribe({
          next: () => {
            this.submittingPriority.set(false);
            this.toastService.success('Đã Xóa', `Đã xóa mức ưu tiên "${p.name}".`);
          },
          error: (err) => {
            this.submittingPriority.set(false);
            this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể xóa mức ưu tiên.'));
          }
        });
      }
    });
  }

  movePriority(currentIndex: number, direction: -1 | 1): void {
    const list = [...this.projectPriorities()];
    const targetIndex = currentIndex + direction;
    if (targetIndex < 0 || targetIndex >= list.length || !this.projectId) return;

    const currentItem = list[currentIndex];
    const targetItem = list[targetIndex];

    const oldLevel = currentItem.level;
    const targetLevel = targetItem.level === oldLevel ? (direction === -1 ? oldLevel - 1 : oldLevel + 1) : targetItem.level;

    this.submittingPriority.set(true);
    this.projectService.updateProjectPriority(this.projectId, currentItem.id, {
      name: currentItem.name,
      level: targetLevel,
      colorHex: currentItem.colorHex,
      iconKey: currentItem.iconKey
    }).subscribe({
      next: () => {
        this.projectService.updateProjectPriority(this.projectId, targetItem.id, {
          name: targetItem.name,
          level: oldLevel,
          colorHex: targetItem.colorHex,
          iconKey: targetItem.iconKey
        }).subscribe({
          next: () => {
            this.submittingPriority.set(false);
            this.toastService.success('Thành công', 'Đã thay đổi thứ tự ưu tiên.');
          },
          error: () => {
            this.submittingPriority.set(false);
            this.projectService.loadProjectPriorities(this.projectId).subscribe();
          }
        });
      },
      error: (err) => {
        this.submittingPriority.set(false);
        this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể cập nhật thứ tự mức ưu tiên.'));
      }
    });
  }
}
