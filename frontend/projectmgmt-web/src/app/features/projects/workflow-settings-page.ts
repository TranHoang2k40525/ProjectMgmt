import { forkJoin } from 'rxjs';
import { apiErrorMessage } from '../../core/http/api-error-message';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  CreateWorkflowTransitionRequest,
  Project,
  ProjectManagementService,
  ProjectStatus,
  WorkflowTransition
} from '../../core/services/project-management.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-workflow-settings-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="workflow-container font-sans text-slate-900 dark:text-slate-100">
      <div class="breadcrumb">
        <a routerLink="/projects">Dự án</a> &gt;
        @if (project()) {
          <a [routerLink]="['/projects', projectId, 'settings']">{{ project()?.name }}</a> &gt;
        }
        <span>Cấu hình Workflow</span>
      </div>

      <div class="page-header">
        <div>
          <h1>Cấu hình Quy trình Workflow</h1>
          <p class="subtitle">Tự do tùy biến các bước trạng thái (Status), cờ hành vi Scrum (Done/Active/Initial) và luật chuyển đổi (Transitions) cho dự án {{ project()?.name }}</p>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="settings-tabs">
        <a [routerLink]="['/projects', projectId, 'settings']" class="tab-item flex items-center gap-1.5">
          <span class="material-symbols-outlined text-[18px]">tune</span>
          <span>Thông tin chung</span>
        </a>
        <span class="tab-item active flex items-center gap-1.5">
          <span class="material-symbols-outlined text-[18px]">sync_alt</span>
          <span>Cấu hình Workflow</span>
        </span>
        <a [routerLink]="['/projects', projectId, 'settings', 'board']" class="tab-item flex items-center gap-1.5">
          <span class="material-symbols-outlined text-[18px]">view_kanban</span>
          <span>Cấu hình Board</span>
        </a>
        <a [routerLink]="['/projects', projectId, 'members']" class="tab-item flex items-center gap-1.5">
          <span class="material-symbols-outlined text-[18px]">badge</span>
          <span>Thành viên & Phân quyền</span>
        </a>
      </div>

      @if (alertMessage()) {
        <div role="status" class="alert" [class.alert-success]="alertType() === 'success'" [class.alert-error]="alertType() === 'error'">
          {{ alertMessage() }}
          @if (alertType() === 'error') {
            <button type="button" class="btn btn-secondary ml-3" (click)="loadProject(); loadWorkflowData()">Tải lại cấu hình</button>
          }
        </div>
      }

      <!-- 1. MẪU WORKFLOW CHUẨN -->
      <div class="card">
        <h3>Áp dụng mẫu quy trình Scrum chuẩn</h3>
        <p class="hint">Bổ sung trạng thái và các bước chuyển tiếp tự động mà vẫn giữ nguyên dữ liệu công việc hiện có.</p>
        <div class="flex flex-col sm:flex-row gap-3 items-stretch sm:items-center">
          <select id="workflowTemplate" class="form-control sm:max-w-xs" [(ngModel)]="selectedTemplate">
            <option value="scrum-std">Scrum tiêu chuẩn (To Do, In Progress, Code Review, Done)</option>
            <option value="agile-qa">Agile QA (To Do, In Progress, Code Review, QA Testing, Done)</option>
            <option value="kanban-simple">Kanban tinh gọn (To Do, In Progress, Done)</option>
            <option value="enterprise">Doanh nghiệp (Backlog, Spec Review, In Progress, Code Review, QA, Done)</option>
          </select>
          <button class="btn btn-primary flex items-center justify-center gap-1.5" [disabled]="loading() || submitting() || !project()" (click)="applyTemplate()">
            <span class="material-symbols-outlined text-[18px]">auto_fix_high</span>
            <span>{{ submitting() ? 'Đang lưu...' : 'Áp dụng mẫu này' }}</span>
          </button>
        </div>
      </div>

      <!-- 2. QUẢN LÝ CÁC TRẠNG THÁI QUY TRÌNH (DYNAMIC STATUSES) -->
      <div class="card">
        <div class="flex items-center justify-between mb-4">
          <div>
            <h3>Các bước trạng thái trong quy trình ({{ statuses().length }} bước)</h3>
            <p class="hint mb-0">Mỗi trạng thái mang cờ hành vi Scrum giúp hệ thống xác định việc hoàn thành, việc dở dang hoặc việc hủy bỏ.</p>
          </div>
          <button type="button" (click)="openAddStatusModal()" class="btn btn-primary flex items-center gap-1.5">
            <span class="material-symbols-outlined text-[18px]">add</span>
            <span>Thêm trạng thái mới</span>
          </button>
        </div>

        <div class="space-y-2">
          @for (s of statuses(); track s.id; let idx = $index) {
            <div class="p-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/60 dark:bg-slate-900/40 flex items-center justify-between gap-4 shadow-2xs">
              <div class="flex items-center gap-3 min-w-0">
                <span class="w-3.5 h-3.5 rounded-full shrink-0 shadow-2xs" [style.backgroundColor]="s.colorHex || '#3B82F6'"></span>
                <div class="min-w-0">
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="font-bold text-slate-800 dark:text-slate-200 text-sm">{{ s.name }}</span>
                    @if (s.isInitial) {
                      <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-sky-50 text-sky-700 border border-sky-200">Khởi tạo</span>
                    }
                    @if (s.isActiveWork) {
                      <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-amber-50 text-amber-700 border border-amber-200">Đang làm</span>
                    }
                    @if (s.isDone) {
                      <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">Hoàn tất (Done)</span>
                    }
                    @if (s.isCanceled) {
                      <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-rose-50 text-rose-700 border border-rose-200">Đã hủy</span>
                    }
                  </div>
                  <p class="text-xs text-slate-400 mt-0.5">Thứ tự cột: {{ s.orderIndex }} | Màu: {{ s.colorHex || '#3B82F6' }}</p>
                </div>
              </div>

              <div class="flex items-center gap-1 shrink-0">
                <button type="button" (click)="moveStatus(idx, -1)" [disabled]="idx === 0" class="p-1.5 rounded-md text-slate-400 hover:text-slate-700 disabled:opacity-20" title="Di chuyển lên">
                  <span class="material-symbols-outlined text-[18px]">arrow_upward</span>
                </button>
                <button type="button" (click)="moveStatus(idx, 1)" [disabled]="idx === statuses().length - 1" class="p-1.5 rounded-md text-slate-400 hover:text-slate-700 disabled:opacity-20" title="Di chuyển xuống">
                  <span class="material-symbols-outlined text-[18px]">arrow_downward</span>
                </button>
                <button type="button" (click)="openEditStatusModal(s)" class="p-1.5 rounded-md text-slate-500 hover:text-slate-800 hover:bg-slate-200/50 transition-colors" title="Chỉnh sửa trạng thái">
                  <span class="material-symbols-outlined text-[18px]">edit</span>
                </button>
                <button type="button" (click)="deleteStatus(s)" class="p-1.5 rounded-md text-red-500 hover:text-red-700 hover:bg-red-50 transition-colors" title="Xóa trạng thái">
                  <span class="material-symbols-outlined text-[18px]">delete</span>
                </button>
              </div>
            </div>
          }
        </div>
      </div>

      <!-- 3. TRANSITIONS & RULES -->
      <div class="workflow-grid">
        <!-- Add Transition Form -->
        <div class="card form-box">
          <h3>Thêm Transition mới</h3>
          <p class="hint">Định nghĩa một đường đi cho phép Issue di chuyển từ trạng thái nguồn sang trạng thái đích.</p>

          <form (ngSubmit)="onCreateTransition()">
            <div class="form-group">
              <label for="fromStatus">Từ trạng thái (From Status) <span class="required">*</span></label>
              <select
                id="fromStatus"
                name="fromStatus"
                [(ngModel)]="newTransition.fromStatusId"
                required
                class="form-control"
              >
                <option value="" disabled>-- Chọn trạng thái nguồn --</option>
                @for (s of statuses(); track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              </select>
            </div>

            <div class="form-group">
              <label for="toStatus">Đến trạng thái (To Status) <span class="required">*</span></label>
              <select
                id="toStatus"
                name="toStatus"
                [(ngModel)]="newTransition.toStatusId"
                required
                class="form-control"
              >
                <option value="" disabled>-- Chọn trạng thái đích --</option>
                @for (s of statuses(); track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              </select>
            </div>

            <div class="form-group">
              <label for="transName">Tên hành động / Tên nút chuyển</label>
              <input
                type="text"
                id="transName"
                name="transName"
                [(ngModel)]="newTransition.name"
                placeholder="VD: Start Work, Submit Review, Resolve..."
                class="form-control"
              />
            </div>

            <div class="form-group">
              <label for="permission">Quyền bắt buộc (Permission Code)</label>
              <select
                id="permission"
                name="permission"
                [(ngModel)]="newTransition.requiredPermissionCode"
                class="form-control"
              >
                <option value="">(Dùng quyền chuyển trạng thái chuẩn)</option>
                <option value="issue.transition">issue.transition (Chuyển trạng thái)</option>
                <option value="project.settings.manage">project.settings.manage (Quản lý cấu hình dự án)</option>
              </select>
              <small class="form-hint">Nếu chọn, chỉ thành viên có quyền này mới thực hiện được bước chuyển này.</small>
            </div>

            <button
              type="submit"
              [disabled]="!project() || loading() || submitting() || !newTransition.fromStatusId || !newTransition.toStatusId"
              class="btn btn-primary btn-block"
            >
              {{ submitting() ? 'Đang thêm...' : '+ Tạo Transition' }}
            </button>
          </form>
        </div>

        <!-- Transitions List -->
        <div class="card list-box">
          <div class="list-header flex items-center justify-between mb-3">
            <h3>Danh sách Transition hiện hành ({{ transitions().length }})</h3>
            <button (click)="loadWorkflowData()" class="btn btn-sm btn-secondary flex items-center gap-1">
              <span class="material-symbols-outlined text-[16px]">refresh</span>
              <span>Làm mới</span>
            </button>
          </div>

          @if (loading()) {
            <div class="loading-state">
              <div class="spinner"></div>
              <p>Đang tải luật chuyển trạng thái...</p>
            </div>
          } @else if (transitions().length === 0) {
            <div class="empty-transitions">
              <p>Chưa có luật chuyển đổi trạng thái nào.</p>
            </div>
          } @else {
            <div class="transitions-table-wrap">
              <table class="transitions-table">
                <thead>
                  <tr>
                    <th>Nguồn (From)</th>
                    <th class="w-10 text-center"><span class="material-symbols-outlined text-[16px] text-slate-400">arrow_forward</span></th>
                    <th>Đích (To)</th>
                    <th>Tên hành động</th>
                    <th>Quyền yêu cầu</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  @for (t of transitions(); track t.id) {
                    <tr>
                      <td>
                        <span class="status-pill pill-from">{{ t.fromStatusName }}</span>
                      </td>
                      <td class="arrow-cell">
                        <span class="material-symbols-outlined text-[16px] text-slate-400">arrow_forward</span>
                      </td>
                      <td>
                        <span class="status-pill pill-to">{{ t.toStatusName }}</span>
                      </td>
                      <td>
                        <strong>{{ t.name || 'Chuyển trạng thái' }}</strong>
                      </td>
                      <td>
                        @if (t.requiredPermissionCode) {
                          <code class="perm-code">{{ t.requiredPermissionCode }}</code>
                        } @else {
                          <span class="perm-none">issue.transition</span>
                        }
                      </td>
                      <td>
                        <button
                          type="button"
                          (click)="onDeleteTransition(t)"
                          class="btn-icon-delete flex items-center justify-center"
                          title="Xóa luật này"
                        >
                          <span class="material-symbols-outlined text-[16px]">delete</span>
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>

      <!-- MODAL TẠO / SỬA TRẠNG THÁI (STATUS) -->
      @if (showStatusModal()) {
        <div class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div class="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl max-w-md w-full p-6 animate-in fade-in zoom-in-95 duration-150">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <span class="material-symbols-outlined text-primary">{{ editingStatus() ? 'edit' : 'add_circle' }}</span>
                <span>{{ editingStatus() ? 'Chỉnh Sửa Trạng Thái' : 'Thêm Trạng Thái Mới' }}</span>
              </h3>
              <button type="button" (click)="showStatusModal.set(false)" class="text-slate-400 hover:text-slate-600">
                <span class="material-symbols-outlined">close</span>
              </button>
            </div>

            <div class="space-y-4">
              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Tên trạng thái <span class="text-red-500">*</span></label>
                <input type="text" [(ngModel)]="statusForm.name" placeholder="Ví dụ: Spec Review, UAT, Blocked..." class="form-control" />
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Mã màu (Hex)</label>
                <div class="flex items-center gap-2 mb-2">
                  <input type="color" [(ngModel)]="statusForm.colorHex" class="w-9 h-9 rounded-lg border border-slate-300 cursor-pointer p-0.5" />
                  <input type="text" [(ngModel)]="statusForm.colorHex" class="form-control font-mono text-xs" />
                </div>
                <div class="flex flex-wrap gap-2 pt-1">
                  @for (col of sampleColors; track col) {
                    <button type="button" (click)="statusForm.colorHex = col" [attr.aria-label]="'Chọn màu ' + col" class="w-6 h-6 rounded-full border-2 border-white shadow-2xs hover:scale-110 transition-transform" [style.backgroundColor]="col">
                      <span class="sr-only">Màu {{ col }}</span>
                    </button>
                  }
                </div>
              </div>

              <div class="pt-2 border-t border-slate-100 dark:border-slate-800 space-y-2.5">
                <p class="text-xs font-bold text-slate-700 dark:text-slate-300 uppercase">Cờ hành vi logic Scrum:</p>

                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="statusForm.isInitial" class="w-4 h-4 rounded text-primary focus:ring-primary" />
                  <span class="text-xs text-slate-700 dark:text-slate-300 font-medium">Khởi tạo (Trạng thái mặc định khi tạo mới task)</span>
                </label>

                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="statusForm.isActiveWork" class="w-4 h-4 rounded text-primary focus:ring-primary" />
                  <span class="text-xs text-slate-700 dark:text-slate-300 font-medium">Đang thực hiện (Tính vào WIP và tiến độ đang làm)</span>
                </label>

                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="statusForm.isDone" class="w-4 h-4 rounded text-primary focus:ring-primary" />
                  <span class="text-xs text-slate-700 dark:text-slate-300 font-medium">Hoàn tất (Tính Story Points hoàn thành cho Sprint / Burndown)</span>
                </label>

                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="statusForm.isCanceled" class="w-4 h-4 rounded text-primary focus:ring-primary" />
                  <span class="text-xs text-slate-700 dark:text-slate-300 font-medium">Hủy bỏ (Công việc bị loại bỏ khỏi phạm vi)</span>
                </label>
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-2">
              <button type="button" (click)="showStatusModal.set(false)" class="btn btn-secondary">Hủy bỏ</button>
              <button type="button" (click)="saveStatus()" [disabled]="!statusForm.name.trim()" class="btn btn-primary">Lưu trạng thái</button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .workflow-container {
      padding: 2rem;
      max-width: 1100px;
      margin: 0 auto;
    }
    .breadcrumb {
      font-size: 0.85rem;
      color: #64748b;
      margin-bottom: 1rem;
    }
    .breadcrumb a {
      color: #1b4332;
      text-decoration: none;
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
      gap: 0.5rem;
      border-bottom: 2px solid #e2e8f0;
      margin: 1.5rem 0;
    }
    .tab-item {
      padding: 0.75rem 1.25rem;
      font-weight: 600;
      font-size: 0.95rem;
      color: #64748b;
      text-decoration: none;
      border-bottom: 2px solid transparent;
      margin-bottom: -2px;
      cursor: pointer;
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
      padding: 1.5rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      margin-bottom: 1.5rem;
    }
    h3 {
      font-size: 1.1rem;
      font-weight: 600;
      color: #0f172a;
      margin: 0 0 0.5rem 0;
    }
    .hint {
      color: #64748b;
      font-size: 0.85rem;
      margin: 0 0 1.25rem 0;
    }
    .workflow-grid {
      display: grid;
      grid-template-columns: 360px 1fr;
      gap: 1.5rem;
    }
    .form-group {
      margin-bottom: 1rem;
    }
    label {
      display: block;
      font-weight: 600;
      color: #334155;
      font-size: 0.85rem;
      margin-bottom: 0.3rem;
    }
    .required {
      color: #ef4444;
    }
    .form-control {
      width: 100%;
      padding: 0.6rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      font-size: 0.9rem;
      box-sizing: border-box;
    }
    .form-control:focus {
      outline: none;
      border-color: #2d6a4f;
      box-shadow: 0 0 0 3px rgba(45, 106, 79, 0.15);
    }
    .form-hint {
      display: block;
      color: #64748b;
      font-size: 0.75rem;
      margin-top: 0.25rem;
    }
    .btn {
      padding: 0.6rem 1.2rem;
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
    .btn-primary:disabled {
      background: #94a3b8;
      cursor: not-allowed;
    }
    .btn-secondary {
      background: #f1f5f9;
      color: #475569;
      border-color: #cbd5e1;
    }
    .btn-secondary:hover {
      background: #e2e8f0;
    }
    .btn-sm {
      padding: 0.35rem 0.75rem;
      font-size: 0.8rem;
    }
    .btn-block {
      width: 100%;
      margin-top: 0.5rem;
    }
    .alert {
      padding: 0.75rem 1rem;
      border-radius: 8px;
      margin-bottom: 1.5rem;
      font-size: 0.9rem;
    }
    .alert-success {
      background: #f0fdf4;
      border: 1px solid #bbf7d0;
      color: #166534;
    }
    .alert-error {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #991b1b;
    }
    .transitions-table-wrap {
      overflow-x: auto;
    }
    .transitions-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.85rem;
    }
    .transitions-table th {
      text-align: left;
      padding: 0.65rem 0.75rem;
      background: #f8fafc;
      color: #475569;
      font-weight: 600;
      border-bottom: 1px solid #e2e8f0;
    }
    .transitions-table td {
      padding: 0.65rem 0.75rem;
      border-bottom: 1px solid #f1f5f9;
      vertical-align: middle;
    }
    .status-pill {
      display: inline-block;
      padding: 0.2rem 0.6rem;
      border-radius: 12px;
      font-weight: 600;
      font-size: 0.8rem;
    }
    .pill-from {
      background: #f1f5f9;
      color: #475569;
    }
    .pill-to {
      background: #e0f2fe;
      color: #0369a1;
    }
    .perm-code {
      background: #f8fafc;
      padding: 0.15rem 0.4rem;
      border-radius: 4px;
      font-size: 0.75rem;
      color: #0f172a;
    }
    .perm-none {
      color: #94a3b8;
      font-style: italic;
      font-size: 0.75rem;
    }
    .btn-icon-delete {
      background: transparent;
      border: none;
      color: #ef4444;
      cursor: pointer;
      padding: 0.3rem;
      border-radius: 4px;
    }
    .btn-icon-delete:hover {
      background: #fef2f2;
    }
    .loading-state, .empty-transitions {
      text-align: center;
      padding: 2.5rem;
      color: #64748b;
    }
    .spinner {
      width: 28px;
      height: 28px;
      border: 3px solid #e2e8f0;
      border-top-color: #2563eb;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 0.75rem auto;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    @media (max-width: 900px) {
      .workflow-grid {
        grid-template-columns: 1fr;
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkflowSettingsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly projectService = inject(ProjectManagementService);
  private readonly confirmService = inject(ConfirmDialogService);
  private readonly toastService = inject(ToastService);

  projectId = '';
  readonly project = signal<Project | null>(null);
  readonly statuses = signal<ProjectStatus[]>([]);
  readonly transitions = signal<WorkflowTransition[]>([]);
  readonly loading = signal<boolean>(true);
  readonly submitting = signal<boolean>(false);
  readonly alertMessage = signal<string | null>(null);
  readonly alertType = signal<'success' | 'error'>('success');

  selectedTemplate = 'scrum-std';

  // Modal Status
  readonly showStatusModal = signal<boolean>(false);
  readonly editingStatus = signal<ProjectStatus | null>(null);
  statusForm = {
    name: '',
    colorHex: '#3B82F6',
    isInitial: false,
    isActiveWork: true,
    isDone: false,
    isCanceled: false
  };

  sampleColors = ['#64748B', '#3B82F6', '#6366F1', '#8B5CF6', '#EC4899', '#F59E0B', '#10B981', '#14B8A6', '#EF4444'];

  newTransition: CreateWorkflowTransitionRequest = {
    fromStatusId: '',
    toStatusId: '',
    name: '',
    requiredPermissionCode: ''
  };

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id') || '';
    if (this.projectId) {
      this.loadProject();
      this.loadWorkflowData();
    }
  }

  loadProject(): void {
    this.projectService.getProjectById(this.projectId).subscribe({
      next: p => this.project.set(p),
      error: err => this.reportError(err)
    });
  }

  loadWorkflowData(): void {
    this.loading.set(true);
    forkJoin({
      statuses: this.projectService.getProjectStatuses(this.projectId),
      transitions: this.projectService.getWorkflowTransitions(this.projectId)
    }).subscribe({
      next: data => {
        this.statuses.set(data.statuses);
        this.transitions.set(data.transitions);
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.reportError(err); }
    });
  }

  private reportError(err: unknown): void {
    this.alertType.set('error');
    this.alertMessage.set(apiErrorMessage(err, 'Không thể tải cấu hình workflow.'));
  }

  applyTemplate(): void {
    if (this.submitting() || this.loading()) return;
    this.submitting.set(true); this.alertMessage.set(null);
    this.projectService.applyWorkflowTemplateToProject(this.projectId, this.selectedTemplate).subscribe({
      next: (statuses) => {
        this.submitting.set(false);
        this.statuses.set(statuses);
        this.alertType.set('success');
        this.alertMessage.set('Đã lưu mẫu workflow vào dự án.');
        this.toastService.success('Mẫu Workflow', 'Đã áp dụng mẫu workflow thành công');
        this.loadWorkflowData();
      },
      error: err => { this.submitting.set(false); this.reportError(err); }
    });
  }

  // --- Dynamic Status CRUD ---
  openAddStatusModal(): void {
    this.editingStatus.set(null);
    this.statusForm = {
      name: '',
      colorHex: '#3B82F6',
      isInitial: false,
      isActiveWork: true,
      isDone: false,
      isCanceled: false
    };
    this.showStatusModal.set(true);
  }

  openEditStatusModal(status: ProjectStatus): void {
    this.editingStatus.set(status);
    this.statusForm = {
      name: status.name,
      colorHex: status.colorHex || '#3B82F6',
      isInitial: !!status.isInitial,
      isActiveWork: !!status.isActiveWork,
      isDone: !!status.isDone,
      isCanceled: !!status.isCanceled
    };
    this.showStatusModal.set(true);
  }

  saveStatus(): void {
    const name = this.statusForm.name.trim();
    if (!name) return;

    const editing = this.editingStatus();
    if (editing) {
      this.projectService.updateWorkflowStatus(this.projectId, editing.id, {
        name,
        colorHex: this.statusForm.colorHex,
        isInitial: this.statusForm.isInitial,
        isActiveWork: this.statusForm.isActiveWork,
        isDone: this.statusForm.isDone,
        isCanceled: this.statusForm.isCanceled
      }).subscribe({
        next: (updated) => {
          this.statuses.update(st => st.map(s => s.id === editing.id ? updated : s));
          this.showStatusModal.set(false);
          this.toastService.success('Trạng Thái', `Đã cập nhật trạng thái "${name}".`);
          this.loadWorkflowData();
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể cập nhật trạng thái.'))
      });
    } else {
      this.projectService.createWorkflowStatus(this.projectId, {
        name,
        colorHex: this.statusForm.colorHex,
        isInitial: this.statusForm.isInitial,
        isActiveWork: this.statusForm.isActiveWork,
        isDone: this.statusForm.isDone,
        isCanceled: this.statusForm.isCanceled
      }).subscribe({
        next: (created) => {
          this.statuses.update(st => [...st, created]);
          this.showStatusModal.set(false);
          this.toastService.success('Trạng Thái', `Đã tạo trạng thái "${name}".`);
          this.loadWorkflowData();
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể tạo trạng thái mới.'))
      });
    }
  }

  deleteStatus(status: ProjectStatus): void {
    this.confirmService.confirm({
      title: 'Xóa Trạng Thái Workflow',
      message: `Bạn có chắc muốn xóa trạng thái "${status.name}"? Nếu còn công việc trong trạng thái này, hệ thống sẽ ngăn chặn xóa.`,
      type: 'danger',
      confirmText: 'Xóa trạng thái',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.projectService.deleteWorkflowStatus(this.projectId, status.id).subscribe({
          next: () => {
            this.statuses.update(st => st.filter(s => s.id !== status.id));
            this.toastService.success('Đã Xóa', `Đã xóa trạng thái "${status.name}".`);
            this.loadWorkflowData();
          },
          error: (err) => this.toastService.error('Lỗi Xóa Trạng Thái', apiErrorMessage(err, 'Không thể xóa trạng thái.'))
        });
      }
    });
  }

  moveStatus(currentIndex: number, direction: -1 | 1): void {
    const list = [...this.statuses()];
    const targetIndex = currentIndex + direction;
    if (targetIndex < 0 || targetIndex >= list.length) return;

    const [moved] = list.splice(currentIndex, 1);
    list.splice(targetIndex, 0, moved);

    const orderedIds = list.map(s => s.id);
    this.statuses.set(list);

    this.projectService.reorderWorkflowStatuses(this.projectId, orderedIds).subscribe({
      next: () => this.toastService.success('Thứ Tự', 'Đã cập nhật thứ tự các trạng thái.'),
      error: (err) => {
        this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể lưu thứ tự.'));
        this.loadWorkflowData();
      }
    });
  }

  // --- Transitions ---
  onCreateTransition(): void {
    this.alertMessage.set(null);
    if (this.submitting() || !this.project() || !this.newTransition.fromStatusId || !this.newTransition.toStatusId) return;

    if (this.newTransition.fromStatusId === this.newTransition.toStatusId) {
      this.alertType.set('error');
      this.alertMessage.set('Lỗi: Trạng thái không thể tự chuyển sang chính nó (Self-transition bị chặn)!');
      this.toastService.error('Lỗi Workflow', 'Trạng thái không thể tự chuyển sang chính nó');
      return;
    }

    const exists = this.transitions().some(
      t => t.fromStatusId === this.newTransition.fromStatusId && t.toStatusId === this.newTransition.toStatusId
    );
    if (exists) {
      this.alertType.set('error');
      this.alertMessage.set('Lỗi: Luật chuyển đổi giữa 2 trạng thái này đã tồn tại (Trùng lặp bị chặn)!');
      this.toastService.error('Lỗi Workflow', 'Luật chuyển đổi giữa 2 trạng thái này đã tồn tại');
      return;
    }

    this.submitting.set(true);
    this.projectService.createWorkflowTransition(this.projectId, this.newTransition).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.alertType.set('success');
        this.alertMessage.set(`Đã thêm thành công luật chuyển "${created.fromStatusName}" -> "${created.toStatusName}"!`);
        this.toastService.success('Thêm Rule Workflow', `Đã tạo luật chuyển "${created.fromStatusName}" -> "${created.toStatusName}"`);
        this.newTransition = { fromStatusId: '', toStatusId: '', name: '', requiredPermissionCode: '' };
        this.loadWorkflowData();
      },
      error: (err) => {
        this.submitting.set(false);
        this.alertType.set('error');
        this.alertMessage.set(apiErrorMessage(err, 'Không thể tạo transition.'));
        this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể tạo transition.'));
      }
    });
  }

  onDeleteTransition(t: WorkflowTransition): void {
    this.confirmService.confirm({
      title: 'Xóa Luật Workflow',
      message: `Bạn có chắc muốn xóa luật chuyển trạng thái từ "${t.fromStatusName}" sang "${t.toStatusName}" không?`,
      type: 'warning',
      confirmText: 'Xóa luật ngay',
      cancelText: 'Hủy bỏ',
      onConfirm: () => {
        this.projectService.deleteWorkflowTransition(t.id).subscribe({
          next: () => {
            this.alertType.set('success');
            this.alertMessage.set('Đã xóa luật chuyển trạng thái thành công.');
            this.toastService.warning('Đã Xóa Rule', `Đã xóa luật chuyển "${t.fromStatusName}" -> "${t.toStatusName}"`);
            this.loadWorkflowData();
          },
          error: (err) => {
            this.alertType.set('error');
            this.alertMessage.set(apiErrorMessage(err, 'Không thể xóa transition.'));
            this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể xóa transition.'));
          }
        });
      }
    });
  }
}
