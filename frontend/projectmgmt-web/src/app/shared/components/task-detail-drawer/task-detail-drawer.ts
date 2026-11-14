import { ChangeDetectorRef, Component, EventEmitter, Input, OnDestroy, Output, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AiBreakdownApi } from '../../../core/api/ai-breakdown.api';
import { ProjectManagementService, WorkItem, ProjectMember } from '../../../core/services/project-management.service';
import { IdentityService } from '../../../core/services/identity.service';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { resolveAvatarUrl } from '../../../core/utils/avatar.utils';
import { extractCiteDocName, filterUnlinkedCitations } from '../../../core/utils/spec-citation.util';

@Component({
  selector: 'app-task-detail-drawer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    @if (task) {
      <div class="task-drawer-backdrop fixed inset-0 z-50 flex justify-end bg-slate-900/40 backdrop-blur-xs transition-opacity animate-fade-in font-body text-slate-900 dark:text-slate-100">
        <!-- Backdrop close click -->
        <button type="button" aria-label="Đóng chi tiết công việc" class="task-drawer-dismiss flex-1 bg-transparent border-0" (click)="dismissed.emit()"></button>

        <!-- Slide-over Right Drawer Container (Resizable Width, Full Width on Split-Screen / Mobile) -->
        <div
          [style.width.px]="isNarrowScreen() ? null : drawerWidth()"
          [class.w-full]="isNarrowScreen()"
          [class.max-w-full]="isNarrowScreen()"
          [class.sm:max-w-full]="isNarrowScreen()"
          class="jira-drawer relative bg-white dark:bg-slate-900 border-l border-slate-200 dark:border-slate-800 shadow-2xl flex flex-col h-full overflow-hidden animate-slide-left transition-all duration-75 w-full max-w-full sm:max-w-[90vw]"
        >
          <!-- Resizable Left Edge Drag Handle (Hidden when Narrow or Resizing not suitable) -->
          @if (!isNarrowScreen()) {
            <div
              (mousedown)="startResizing($event)"
              title="Kéo thả để mở rộng hoặc thu gọn độ rộng bảng chi tiết"
              class="hidden sm:flex absolute left-0 top-0 bottom-0 w-2 hover:w-3 bg-slate-200 hover:bg-primary dark:bg-slate-800 cursor-col-resize z-50 items-center justify-center transition-all group"
            >
              <div class="w-0.5 h-8 bg-slate-400 group-hover:bg-white rounded-full"></div>
            </div>
          }
          
          <!-- Drawer Top Navigation Header -->
          <div class="task-drawer-header flex items-center justify-between px-4 sm:px-5 py-2.5 sm:py-3 border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shrink-0">
            <div class="flex items-center gap-2 min-w-0 flex-wrap">
              <span
                class="w-6 h-6 rounded-md inline-flex items-center justify-center text-white shrink-0 shadow-2xs"
                [style.backgroundColor]="getIssueTypeColor(task)"
                [title]="task.issueType"
              >
                <span class="material-symbols-outlined text-[15px]">{{ getIssueTypeIcon(task) }}</span>
              </span>
              <span class="px-2 py-0.5 rounded text-xs font-mono font-bold bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shrink-0" [style.color]="getIssueTypeColor(task)">
                {{ task.issueKey }}
              </span>
              @if (task.parentId) {
                <span class="px-2 py-0.5 rounded text-[11px] font-semibold bg-indigo-50 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800 shrink-0">
                  {{ task.subtaskRole ? ('Việc con · ' + task.subtaskRole) : 'Việc con' }}
                </span>
                @if (parentTask(); as parent) {
                  <button
                    type="button"
                    (click)="openRelatedTask(parent)"
                    class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 border border-slate-200 dark:border-slate-700 max-w-[240px] truncate cursor-pointer transition-colors"
                    [title]="'Quay lại việc cha: ' + parent.issueKey + ' - ' + parent.title"
                  >
                    <span class="material-symbols-outlined text-[13px]">arrow_back</span>
                    <span class="truncate">{{ parent.issueKey }} · {{ parent.title }}</span>
                  </button>
                }
              } @else {
                <span class="text-xs text-slate-500 font-medium truncate">{{ task.sprintName ? ('Sprint: ' + task.sprintName) : (projectService.currentProject().name || 'Dự án') }}</span>
              }
            </div>

            <div class="flex items-center gap-1 shrink-0">
              <button (click)="cloneTask()" class="w-8 h-8 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 flex items-center justify-center transition-colors cursor-pointer" title="Nhân bản">
                <span class="material-symbols-outlined text-[18px]">content_copy</span>
              </button>
              <button (click)="deleteTask()" class="w-8 h-8 rounded-lg hover:bg-rose-50 dark:hover:bg-rose-950/40 text-slate-500 hover:text-rose-600 dark:hover:text-rose-400 flex items-center justify-center transition-colors cursor-pointer" title="Xóa">
                <span class="material-symbols-outlined text-[18px]">delete</span>
              </button>
              <div class="h-3.5 w-px bg-slate-200 dark:bg-slate-800 mx-1"></div>
              <button (click)="dismissed.emit()" class="w-8 h-8 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 flex items-center justify-center transition-colors cursor-pointer" title="Đóng">
                <span class="material-symbols-outlined text-[18px]">close</span>
              </button>
            </div>
          </div>

          <!-- Drawer Main Content Body -->
          <div class="task-drawer-body flex-1 overflow-y-auto p-4 sm:p-5 flex flex-col gap-4 bg-white dark:bg-slate-900">
            
            <!-- Issue Title & Type Bar -->
            <div class="flex flex-col gap-2">
              <div class="flex items-center gap-2 flex-wrap">
                <!-- Issue Type Selector -->
                <select
                  [(ngModel)]="task.issueType"
                  (ngModelChange)="onFieldChange()"
                  class="h-7 px-2.5 rounded text-xs font-semibold bg-white text-slate-700 dark:bg-slate-900 dark:text-slate-200 border border-slate-200 dark:border-slate-700 focus:outline-none focus:border-slate-900 cursor-pointer transition-colors"
                >
                  @if (task.issueType && !availableIssueTypes().includes(task.issueType)) {
                    <option [value]="task.issueType">{{ task.issueType }}</option>
                  }
                  @for (t of availableIssueTypes(); track t) {
                    <option [value]="t">{{ t }}</option>
                  }
                </select>

                <!-- Epic Selector -->
                <select
                  [ngModel]="task.epicName || ''"
                  (ngModelChange)="task.epicName = $event; onFieldChange()"
                  class="h-7 px-2.5 rounded text-xs font-medium bg-white text-slate-600 dark:bg-slate-900 dark:text-slate-300 border border-slate-200 dark:border-slate-700 focus:outline-none focus:border-slate-900 cursor-pointer transition-colors"
                >
                  <option value="">Gán Epic</option>
                  @if (task.epicName && !hasEpicName(task.epicName)) {
                    <option [value]="task.epicName">{{ task.epicName }}</option>
                  }
                  @for (epic of epics(); track epic.id) {
                    <option [value]="epic.name">{{ epic.name }}</option>
                  }
                </select>

                <span class="text-xs text-slate-400 ml-auto font-mono">{{ task.createdAt | date:'dd/MM/yyyy HH:mm' }}</span>
              </div>

              <input
                type="text"
                [(ngModel)]="task.title"
                (ngModelChange)="onFieldChange()"
                placeholder="Tên công việc"
                class="text-lg font-semibold text-slate-900 dark:text-white bg-transparent border-b border-transparent hover:border-slate-200 focus:border-slate-900 focus:outline-none py-1 transition-all"
              />
            </div>

            <!-- Spec Links & Document Citations Card (for both Parent & Subtask) -->
            @if ((task.specLinks && task.specLinks.length > 0) || (task.documentCitations && task.documentCitations.length > 0)) {
              <div class="p-3 rounded-lg bg-slate-50/90 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-700/80 flex flex-col gap-2">
                <div class="flex items-center gap-1.5 text-xs font-semibold text-slate-700 dark:text-slate-200">
                  <span class="material-symbols-outlined text-[15px] text-primary">description</span>
                  <span>Tài liệu căn cứ &amp; Trích dẫn đặc tả</span>
                </div>
                <div class="flex flex-wrap gap-1.5">
                  @for (link of task.specLinks || []; track link.documentId) {
                    <a
                      [routerLink]="projectService.currentProject().id ? ['/projects', projectService.currentProject().id, 'documents'] : ['/documents']"
                      [queryParams]="{ docId: link.documentId, docName: link.documentTitle || link.fileName }"
                      (click)="dismissed.emit()"
                      class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-300 dark:border-emerald-800 transition-colors"
                      [title]="link.excerpt || link.documentTitle"
                    >
                      <span class="material-symbols-outlined text-[14px]">description</span>
                      <span class="truncate max-w-[280px]">{{ link.excerpt || link.documentTitle || link.fileName }}</span>
                      <span class="material-symbols-outlined text-[13px]">open_in_new</span>
                    </a>
                  }
                  @for (cite of unlinkedCitations(); track $index) {
                    <a
                      [routerLink]="projectService.currentProject().id ? ['/projects', projectService.currentProject().id, 'documents'] : ['/documents']"
                      [queryParams]="{ docName: extractCiteDocName(cite) }"
                      (click)="dismissed.emit()"
                      class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium bg-white hover:bg-slate-100 text-slate-700 border border-slate-200 dark:bg-slate-900 dark:text-slate-300 dark:border-slate-700 transition-colors"
                      [title]="'Mở trong Spec Hub: ' + cite"
                    >
                      <span class="material-symbols-outlined text-[14px] text-primary">article</span>
                      <span class="truncate max-w-[280px]">{{ cite }}</span>
                      <span class="material-symbols-outlined text-[13px]">open_in_new</span>
                    </a>
                  }
                </div>
              </div>
            }

            <!-- Blocker Status Bar -->
            @if (task.isBlocked) {
              <div class="p-2.5 rounded bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-rose-800 dark:text-rose-200 flex items-center justify-between gap-3">
                <div class="flex items-center gap-2 min-w-0">
                  <span class="material-symbols-outlined text-rose-600 dark:text-rose-400 text-[18px] shrink-0">block</span>
                  <div class="flex flex-col min-w-0">
                    <span class="text-xs font-bold text-rose-700 dark:text-rose-300">Cản trở</span>
                    <span class="text-xs font-medium truncate">{{ task.blockReason || 'Không có lý do chi tiết' }}</span>
                  </div>
                </div>
                <button
                  type="button"
                  (click)="unblockTask()"
                  class="h-6 px-2 rounded bg-rose-600 hover:bg-rose-700 text-white font-medium text-xs shrink-0 flex items-center gap-1 cursor-pointer transition-colors"
                >
                  <span class="material-symbols-outlined text-[13px]">check</span>
                  <span>Gỡ cản trở</span>
                </button>
              </div>
            } @else {
              <div class="flex items-center justify-between py-0.5">
                <span class="text-xs text-slate-500 flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[15px] text-emerald-600">check_circle</span>
                  <span>Thông suốt</span>
                </span>
                <button
                  type="button"
                  (click)="openBlockTaskModal()"
                  class="h-6 px-2 rounded text-xs font-medium text-slate-600 hover:text-rose-600 hover:bg-rose-50 border border-slate-200 hover:border-rose-200 flex items-center gap-1 cursor-pointer transition-colors"
                >
                  <span class="material-symbols-outlined text-[13px]">block</span>
                  <span>Báo cản trở</span>
                </button>
              </div>
            }

            <!-- Metadata Properties Grid (Responsive 1/2/3 cols for half-screen & mobile) -->
            <div class="task-meta-grid grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 p-3.5 rounded-xl bg-slate-50/80 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800">
              <!-- Status Dropdown -->
              <div class="flex flex-col gap-1 min-w-0">
                <span class="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Trạng thái</span>
                <select
                  [(ngModel)]="task.statusName"
                  (ngModelChange)="onFieldChange()"
                  class="h-8 px-2.5 rounded-lg text-xs font-semibold border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary shadow-2xs cursor-pointer w-full"
                >
                  @if (task.statusName && !availableStatusNames().includes(task.statusName)) {
                    <option [value]="task.statusName">{{ task.statusName }}</option>
                  }
                  @for (st of availableStatusNames(); track st) {
                    <option [value]="st">{{ st }}</option>
                  }
                </select>
              </div>

              <!-- Priority -->
              <div class="flex flex-col gap-1 min-w-0">
                <span class="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Độ ưu tiên</span>
                <select
                  [(ngModel)]="task.priority"
                  (ngModelChange)="onFieldChange()"
                  class="h-8 px-2.5 rounded-lg text-xs font-semibold border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary shadow-2xs cursor-pointer w-full"
                >
                  @if (task.priority && !hasPriorityOption(task.priority)) {
                    <option [value]="task.priority">{{ task.priority }}</option>
                  }
                  @for (p of priorityOptions(); track p.value) {
                    <option [value]="p.value">{{ p.label }}</option>
                  }
                </select>
              </div>

              <!-- Assignee -->
              <div class="flex flex-col gap-1 min-w-0">
                <span class="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Người thực hiện</span>
                <div class="flex items-center gap-1.5 w-full min-w-0">
                  @if (getAssigneeAvatar(task.assigneeName); as av) {
                    <img [src]="av" [alt]="task.assigneeName || 'Avatar'" class="w-6 h-6 rounded-full object-cover shrink-0 border border-slate-200 dark:border-slate-700" />
                  } @else if (task.assigneeName) {
                    <span class="w-6 h-6 rounded-full bg-slate-800 text-white font-bold text-[10px] flex items-center justify-center shrink-0">
                      {{ getInitials(task.assigneeName) }}
                    </span>
                  }
                  <select
                    [ngModel]="task.assigneeName || ''"
                    (ngModelChange)="task.assigneeName = $event; onFieldChange()"
                    class="h-8 px-2 rounded-lg text-xs font-medium border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary flex-1 min-w-0 shadow-2xs cursor-pointer"
                  >
                    <option value="">Chưa chỉ định</option>
                    @for (m of projectMembers(); track m.id) {
                      <option [value]="m.displayName">{{ m.displayName }}</option>
                    }
                  </select>
                </div>
              </div>

              <!-- Sprint Selector -->
              <div class="flex flex-col gap-1 min-w-0">
                <span class="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Chu kỳ Sprint</span>
                <select
                  [ngModel]="task.sprintId || ''"
                  (ngModelChange)="onSprintChange($event)"
                  class="h-8 px-2.5 rounded-lg text-xs font-medium border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary shadow-2xs cursor-pointer w-full"
                >
                  <option value="">Product Backlog</option>
                  @for (sp of availableSprints(); track sp.id) {
                    <option [value]="sp.id">{{ sp.name }}</option>
                  }
                </select>
              </div>

              <!-- Story Points -->
              <div class="flex flex-col gap-1 min-w-0">
                <span class="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Story Points</span>
                <input
                  type="number"
                  min="0"
                  max="100"
                  [(ngModel)]="task.storyPoints"
                  (ngModelChange)="onFieldChange()"
                  class="h-8 w-full px-2.5 rounded-lg text-xs font-mono font-bold border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary shadow-2xs"
                />
              </div>

              <!-- Estimated Hours -->
              <div class="flex flex-col gap-1 min-w-0">
                <span class="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Giờ ước tính (h)</span>
                <input
                  type="number"
                  min="0"
                  step="0.5"
                  [(ngModel)]="task.estimatedHours"
                  (ngModelChange)="onFieldChange()"
                  placeholder="0.0"
                  class="h-8 w-full px-2.5 rounded-lg text-xs font-mono font-medium border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary shadow-2xs"
                />
              </div>

              <!-- Remaining Hours -->
              <div class="flex flex-col gap-1 min-w-0">
                <span class="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Giờ còn lại (h)</span>
                <input
                  type="number"
                  min="0"
                  step="0.5"
                  [(ngModel)]="task.remainingHours"
                  (ngModelChange)="onFieldChange()"
                  placeholder="0.0"
                  class="h-8 w-full px-2.5 rounded-lg text-xs font-mono font-medium border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary shadow-2xs"
                />
              </div>
            </div>

            <!-- Task Description -->
            <div class="flex flex-col gap-1.5">
              <span class="text-xs font-medium text-slate-500">Mô tả chi tiết</span>
              <textarea
                rows="3"
                [(ngModel)]="task.description"
                (ngModelChange)="onFieldChange()"
                placeholder="Mô tả công việc..."
                class="w-full p-2.5 rounded bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-slate-900 transition-all leading-relaxed"
              ></textarea>
            </div>

            <!-- Labels & Required Skills (Full parity with AI Proposal for both Parent & Subtask) -->
            <div class="flex flex-col gap-2.5 pt-3 border-t border-slate-200 dark:border-slate-800">
              <div class="flex items-center justify-between gap-2">
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[15px] text-slate-500">sell</span>
                  <span>Nhãn phân loại &amp; Kỹ năng</span>
                </span>
                <button
                  type="button"
                  (click)="isTagEditorOpen.set(!isTagEditorOpen())"
                  class="text-xs font-medium text-slate-600 hover:text-slate-900 dark:text-slate-400 flex items-center gap-0.5 cursor-pointer"
                >
                  <span class="material-symbols-outlined text-[13px]">add</span>
                  <span>Thêm nhãn</span>
                </button>
              </div>

              @if (isTagEditorOpen()) {
                <div class="flex flex-wrap gap-1.5 p-2 rounded bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-700">
                  <input
                    type="text"
                    [ngModel]="newTagText()"
                    (ngModelChange)="newTagText.set($event)"
                    (keyup.enter)="addLabelFromDrawer()"
                    placeholder="Nhập nhãn mới (VD: backend, realtime)..."
                    class="flex-1 min-w-[160px] h-7 px-2.5 rounded bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-xs focus:outline-none focus:border-slate-900"
                  />
                  <button type="button" (click)="addLabelFromDrawer()" class="h-7 px-2.5 rounded bg-slate-900 text-white text-xs font-medium hover:bg-slate-800 cursor-pointer">Thêm</button>
                </div>
              }

              <div class="flex flex-wrap items-center gap-1.5">
                @for (lbl of task.labels || []; track lbl) {
                  <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-medium bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200 border border-slate-200 dark:border-slate-700">
                    <span>#{{ lbl }}</span>
                    <button type="button" (click)="removeLabelFromDrawer(lbl)" class="text-slate-400 hover:text-rose-600 leading-none cursor-pointer" [attr.aria-label]="'Bỏ nhãn ' + lbl">
                      <span class="material-symbols-outlined text-[12px]">close</span>
                    </button>
                  </span>
                }
                @for (skill of task.requiredSkills || []; track skill) {
                  <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-violet-50 text-violet-700 dark:bg-violet-950/50 dark:text-violet-300 border border-violet-200 dark:border-violet-800">
                    <span class="material-symbols-outlined text-[12px]">psychology</span>
                    <span>{{ skill }}</span>
                  </span>
                }
                @if ((task.labels?.length || 0) === 0 && (task.requiredSkills?.length || 0) === 0) {
                  <span class="text-xs text-slate-400 italic">Chưa gắn nhãn hoặc kỹ năng yêu cầu.</span>
                }
              </div>
            </div>

            <!-- Acceptance Criteria Checklist (Interactive for both Parent & Subtask) -->
            <div class="flex flex-col gap-2 pt-3 border-t border-slate-200 dark:border-slate-800">
              <div class="flex items-center justify-between">
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[15px] text-emerald-600">fact_check</span>
                  <span>Tiêu chí nghiệm thu ({{ (task.criteria || []).length }})</span>
                </span>
              </div>

              <div class="space-y-1.5">
                @for (c of task.criteria || []; track c.id) {
                  <div class="flex items-center gap-2 p-2 rounded bg-slate-50/70 dark:bg-slate-800/40 border border-slate-200/80 dark:border-slate-800 text-xs">
                    <input
                      type="checkbox"
                      [checked]="c.isMet"
                      (change)="toggleCriterion(c)"
                      [disabled]="isCriterionBusy()"
                      class="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500 cursor-pointer shrink-0"
                    />
                    <input
                      type="text"
                      [value]="c.content"
                      (blur)="updateCriterionText(c, $any($event.target).value)"
                      [disabled]="isCriterionBusy()"
                      [class.line-through]="c.isMet"
                      [class.text-slate-400]="c.isMet"
                      class="flex-1 min-w-0 bg-transparent border-0 text-xs text-slate-800 dark:text-slate-200 focus:outline-none"
                    />
                    <button
                      type="button"
                      (click)="deleteCriterionFromDrawer(c.id)"
                      [disabled]="isCriterionBusy()"
                      class="text-slate-400 hover:text-rose-600 p-0.5 cursor-pointer shrink-0"
                      title="Xóa tiêu chí"
                    >
                      <span class="material-symbols-outlined text-[14px]">close</span>
                    </button>
                  </div>
                } @empty {
                  <p class="text-xs text-slate-400 italic">Chưa có tiêu chí nghiệm thu.</p>
                }
              </div>

              <div class="flex gap-1.5">
                <input
                  type="text"
                  [ngModel]="newCriterionText()"
                  (ngModelChange)="newCriterionText.set($event)"
                  (keyup.enter)="addCriterionFromDrawer()"
                  [disabled]="isCriterionBusy()"
                  placeholder="Thêm tiêu chí nghiệm thu mới..."
                  class="flex-1 h-7 px-2.5 rounded bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-xs focus:outline-none focus:border-slate-900"
                />
                <button
                  type="button"
                  (click)="addCriterionFromDrawer()"
                  [disabled]="isCriterionBusy() || !newCriterionText().trim()"
                  class="h-7 px-2.5 rounded bg-slate-900 disabled:opacity-40 text-white text-xs font-medium hover:bg-slate-800 cursor-pointer"
                >
                  Thêm
                </button>
              </div>
            </div>

            <!-- AI Rationale Card (when item was enriched by AI) -->
            @if (task.aiRationale) {
              <div class="p-3 rounded-lg bg-emerald-50/70 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800/80 flex flex-col gap-1">
                <div class="flex items-center gap-1.5 text-xs font-semibold text-emerald-800 dark:text-emerald-300">
                  <span class="material-symbols-outlined text-[15px]">auto_awesome</span>
                  <span>Lập luận phân tích AI</span>
                </div>
                <p class="text-xs text-emerald-900/90 dark:text-emerald-200/90 leading-relaxed m-0">{{ task.aiRationale }}</p>
              </div>
            }

            <!-- Sub-tasks Section -->
            @if (!task.parentId || drawerSubtasks.length > 0) {
              <div class="flex flex-col gap-2 pt-3 border-t border-slate-200 dark:border-slate-800">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                    <span class="material-symbols-outlined text-[15px] text-slate-500">subdirectory_arrow_right</span>
                    <span>Công việc con ({{ drawerSubtasks.length }})</span>
                  </span>
                  <button (click)="isSubtaskComposerOpen.set(!isSubtaskComposerOpen())" class="text-xs font-medium text-slate-600 hover:text-slate-900 dark:text-slate-400 flex items-center gap-1 cursor-pointer">
                    <span class="material-symbols-outlined text-[13px]">add</span>
                    <span>Thêm việc con</span>
                  </button>
                </div>

                @if (isSubtaskComposerOpen()) {
                  <div class="flex flex-col sm:flex-row gap-2 p-2 rounded bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-700">
                    <input
                      type="text"
                      [ngModel]="newSubtaskTitle()"
                      (ngModelChange)="newSubtaskTitle.set($event)"
                      (keyup.enter)="addSubtaskFromDrawer()"
                      placeholder="Tên việc con..."
                      class="flex-1 h-7 px-2.5 rounded bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-xs focus:outline-none focus:border-slate-900"
                    />
                    <div class="flex gap-1">
                      <button (click)="addSubtaskFromDrawer()" class="h-7 px-2.5 rounded bg-slate-900 text-white text-xs font-medium hover:bg-slate-800 cursor-pointer">Thêm</button>
                      <button (click)="cancelSubtaskComposer()" class="h-7 px-2 rounded bg-white border border-slate-200 text-slate-600 text-xs font-medium hover:bg-slate-50 cursor-pointer">Hủy</button>
                    </div>
                  </div>
                }

                <div class="space-y-1.5">
                  @for (sub of drawerSubtasks; track sub.id) {
                    <div
                      (click)="openRelatedTask(sub)"
                      title="Nhấp để xem &amp; chỉnh sửa chi tiết việc con"
                      class="drawer-subtask-row flex items-center justify-between gap-2 p-2 rounded-lg bg-white hover:bg-slate-50 dark:bg-slate-900 dark:hover:bg-slate-800/70 border border-slate-200 hover:border-slate-300 dark:border-slate-800 text-xs cursor-pointer transition-colors"
                    >
                      <div class="flex items-center gap-2 min-w-0 flex-1">
                        <span class="font-mono text-slate-500 text-[11px] shrink-0">{{ sub.issueKey }}</span>
                        @if (sub.subtaskRole) {
                          <span class="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-indigo-50 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300 shrink-0">{{ sub.subtaskRole }}</span>
                        }
                        <span class="font-medium text-slate-800 dark:text-slate-200 truncate">{{ sub.title }}</span>
                      </div>
                      <div class="flex items-center gap-1.5 shrink-0">
                        @if (sub.priority) {
                          <span class="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300 border border-amber-200/80">{{ sub.priority }}</span>
                        }
                        <span class="px-1.5 py-0.5 rounded text-[10px] font-mono font-semibold bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300">{{ sub.storyPoints }} SP</span>
                        @if (sub.estimatedHours) {
                          <span class="text-[10px] font-mono text-slate-500">{{ sub.estimatedHours }}h</span>
                        }
                        <span class="px-1.5 py-0.5 rounded text-[10px] font-medium bg-slate-100 text-slate-600 border border-slate-200 dark:bg-slate-800 dark:text-slate-300 dark:border-slate-700">{{ sub.statusName }}</span>
                        <span class="text-xs text-slate-500 max-w-[110px] truncate">{{ sub.assigneeName || 'Chưa gán' }}</span>
                        <span class="material-symbols-outlined text-[15px] text-slate-400">chevron_right</span>
                      </div>
                    </div>
                  }

                  @if (drawerSubtasks.length === 0) {
                    <p class="text-xs text-slate-400 italic py-1">Chưa có công việc con.</p>
                  }
                </div>
              </div>
            }

            <!-- Comments Section -->
            <div class="flex flex-col gap-2.5 pt-3 border-t border-slate-200 dark:border-slate-800">
              <div class="flex items-center justify-between">
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[15px] text-slate-500">chat_bubble_outline</span>
                  <span>Bình luận ({{ localComments().length }})</span>
                </span>
                <a [routerLink]="['/issues', task.id]" (click)="dismissed.emit()" class="text-xs text-slate-500 hover:text-slate-900 underline flex items-center gap-1">
                  <span>Trang chi tiết</span>
                  <span class="material-symbols-outlined text-[12px]">open_in_new</span>
                </a>
              </div>

              <!-- Comments List (Newest first) -->
              <div class="flex flex-col gap-2 mt-1">
                @for (comment of sortedComments(); track comment.id) {
                  <div class="flex gap-2.5 p-2.5 rounded-md bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-800">
                    @if (getAuthorAvatar(comment.authorName, comment.authorAvatar); as av) {
                      <img [src]="av" [alt]="comment.authorName" class="w-6 h-6 rounded-full object-cover shrink-0 border border-slate-200 dark:border-slate-700" />
                    } @else {
                      <div class="w-6 h-6 rounded bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-200 flex items-center justify-center text-[10px] font-bold shrink-0">
                        {{ comment.authorName[0] }}
                      </div>
                    }
                    <div class="flex flex-col gap-0.5 text-xs min-w-0 flex-1">
                      <div class="flex items-center gap-2">
                        <span class="font-semibold text-slate-900 dark:text-slate-100">{{ comment.authorName }}</span>
                        <span class="text-[10px] text-slate-400 font-mono">{{ comment.createdAt | date:'dd/MM/yyyy HH:mm' }}</span>
                      </div>
                      <p class="text-slate-700 dark:text-slate-300 leading-relaxed break-words whitespace-pre-wrap">{{ comment.content }}</p>
                    </div>
                  </div>
                } @empty {
                  <p class="text-xs text-slate-400 py-1 italic">Chưa có bình luận nào.</p>
                }
              </div>
            </div>

            <!-- Attachments Section (Images and Videos stored in Attachments_Hub) -->
            @if (task.attachments && task.attachments.length > 0) {
              <div class="flex flex-col gap-2 pt-3 border-t border-slate-200 dark:border-slate-800">
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[15px] text-slate-500">attachment</span>
                  <span>Tệp đính kèm ({{ task.attachments.length }})</span>
                </span>
                <div class="grid grid-cols-2 sm:grid-cols-3 gap-2">
                  @for (att of task.attachments; track att.id) {
                    <div class="rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-850 p-2 flex flex-col gap-1 overflow-hidden">
                      <div class="flex items-center gap-1.5 min-w-0">
                        <span class="material-symbols-outlined text-[16px] text-primary shrink-0">
                          {{ isVideoAttachment(att.fileName) ? 'movie' : 'image' }}
                        </span>
                        <span class="text-xs font-medium text-slate-800 dark:text-slate-200 truncate" [title]="att.fileName">{{ att.fileName }}</span>
                      </div>
                      <span class="text-[10px] text-slate-400 font-mono">{{ formatFileSize(att.fileSizeBytes) }}</span>
                      <a [href]="getAttachmentDownloadUrl(att.id)" target="_blank" class="text-[11px] font-semibold text-primary hover:underline flex items-center gap-0.5 mt-0.5">
                        <span class="material-symbols-outlined text-[13px]">download</span>
                        <span>Tải về</span>
                      </a>
                    </div>
                  }
                </div>
              </div>
            }

          </div>

          <!-- Pinned Fixed Bottom Comment Bar (Not scrolling with body) -->
          <div class="pinned-comment-bar shrink-0 p-3 bg-slate-50 dark:bg-slate-950 border-t border-slate-200 dark:border-slate-800 relative z-30">
            <!-- Mention Autocomplete Floating Dropdown -->
            @if (showMentionDropdown() && mentionCandidates().length > 0) {
              <div class="absolute bottom-full left-3 mb-2 w-64 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-xl overflow-hidden z-40 max-h-48 overflow-y-auto">
                <div class="p-2 text-[10px] font-bold text-slate-400 uppercase tracking-wider bg-slate-50 dark:bg-slate-850 border-b border-slate-100 dark:border-slate-800">Nhắc tên thành viên (&#64;)</div>
                @for (m of mentionCandidates(); track m.userId) {
                  <button
                    type="button"
                    (click)="insertMention(m)"
                    class="w-full px-3 py-1.5 flex items-center gap-2 hover:bg-slate-100 dark:hover:bg-slate-800 text-left text-xs cursor-pointer transition-colors"
                  >
                    @if (getMemberAvatar(m); as av) {
                      <img [src]="av" [alt]="m.displayName" class="w-5 h-5 rounded-full object-cover shrink-0" />
                    } @else {
                      <div class="w-5 h-5 rounded-full bg-primary/10 text-primary font-bold text-[10px] flex items-center justify-center shrink-0">
                        {{ getInitials(m.displayName) }}
                      </div>
                    }
                    <div class="flex flex-col min-w-0">
                      <span class="font-bold text-slate-800 dark:text-slate-200 truncate">{{ m.displayName }}</span>
                      <span class="text-[10px] text-slate-400 truncate">{{ m.email }}</span>
                    </div>
                  </button>
                }
              </div>
            }

            <div class="flex items-center gap-2">
              @if (currentUserAvatar(); as av) {
                <img [src]="av" [alt]="'Tôi'" class="w-7 h-7 rounded-full object-cover shrink-0 border border-slate-200 dark:border-slate-700" />
              } @else {
                <div class="w-7 h-7 rounded-full bg-slate-900 text-white flex items-center justify-center text-[11px] font-semibold shrink-0">
                  {{ currentUserInitial() }}
                </div>
              }

              <div class="flex-1 relative flex items-center">
                <input
                  type="text"
                  [ngModel]="newCommentText"
                  (ngModelChange)="onCommentInputChange($event)"
                  (keyup.enter)="submitComment()"
                  placeholder="Viết bình luận... Gõ @ để tag thành viên (Enter để gửi)"
                  class="w-full h-9 pl-3 pr-20 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary shadow-2xs"
                />

                <div class="absolute right-1 flex items-center gap-1">
                  <!-- Attachment Upload Button -->
                  <input
                    type="file"
                    id="task-attachment-input"
                    accept="image/*,video/mp4,video/quicktime,video/webm"
                    (change)="onAttachmentSelected($event)"
                    class="hidden"
                  />
                  <button
                    type="button"
                    (click)="triggerAttachmentUpload()"
                    [disabled]="isUploadingAttachment()"
                    class="w-7 h-7 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-500 hover:text-slate-800 flex items-center justify-center cursor-pointer transition-colors"
                    title="Đính kèm hình ảnh hoặc video ngắn (< 50MB)"
                  >
                    @if (isUploadingAttachment()) {
                      <span class="material-symbols-outlined text-[16px] animate-spin">sync</span>
                    } @else {
                      <span class="material-symbols-outlined text-[17px]">attach_file</span>
                    }
                  </button>

                  <!-- Send Comment Button -->
                  <button
                    type="button"
                    (click)="submitComment()"
                    [disabled]="!newCommentText.trim()"
                    class="w-7 h-7 rounded-lg bg-primary hover:bg-primary-container disabled:opacity-40 text-white flex items-center justify-center cursor-pointer transition-colors"
                    title="Gửi bình luận"
                  >
                    <span class="material-symbols-outlined text-[16px]">send</span>
                  </button>
                </div>
              </div>
            </div>
          </div>

        </div>

        <!-- Modal Báo Cản Trở (Blocker Dialog) -->
        @if (isBlockModalOpen()) {
          <div class="fixed inset-0 z-60 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs animate-fade-in">
            <div class="w-full max-w-md bg-white dark:bg-slate-900 rounded shadow-xl border border-slate-200 dark:border-slate-800 p-4 flex flex-col gap-3">
              <div class="flex items-center gap-2 text-rose-600 dark:text-rose-400">
                <span class="material-symbols-outlined text-[20px]">block</span>
                <h3 class="text-sm font-semibold text-slate-900 dark:text-white">Báo cáo cản trở</h3>
              </div>
              <textarea
                rows="3"
                [ngModel]="blockReasonInput()"
                (ngModelChange)="blockReasonInput.set($event)"
                placeholder="Nhập lý do bị cản trở..."
                class="w-full p-2.5 rounded border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-rose-500"
              ></textarea>
              <div class="flex justify-end gap-2 pt-1 border-t border-slate-100 dark:border-slate-800">
                <button
                  type="button"
                  (click)="closeBlockTaskModal()"
                  class="h-7 px-3 rounded text-xs font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer border border-slate-200"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  (click)="submitBlockTask()"
                  [disabled]="!blockReasonInput().trim()"
                  class="h-7 px-3 rounded bg-rose-600 hover:bg-rose-700 disabled:opacity-50 text-white text-xs font-medium flex items-center gap-1 cursor-pointer transition-colors"
                >
                  <span class="material-symbols-outlined text-[14px]">check</span>
                  <span>Xác nhận</span>
                </button>
              </div>
            </div>
          </div>
        }
      </div>
    }
  `,
  styles: [`
    @media (max-width: 1279px) {
      .jira-drawer { width: min(680px, 100vw) !important; max-width: 100vw !important; }
      .task-drawer-header, .task-drawer-body, .task-meta-grid, .drawer-subtask-row { min-width: 0; }
      .jira-drawer button, .jira-drawer input, .jira-drawer select, .jira-drawer textarea { min-height: 40px; }
    }

    @media (max-width: 767px) {
      .task-drawer-dismiss { display: none; }
      .jira-drawer { width: 100vw !important; border-left: 0; }
      .task-drawer-header { align-items: flex-start; gap: .75rem; padding: .75rem; }
      .task-drawer-header > div:first-child { align-items: flex-start; flex-direction: column; gap: .35rem; min-width: 0; }
      .task-drawer-body { gap: 1rem; padding: .75rem; }
      .task-meta-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; padding: .75rem; }
      .task-meta-grid input { width: 100%; }
      .drawer-subtask-row { align-items: flex-start; flex-direction: column; gap: .5rem; }
      .drawer-subtask-row > div { min-width: 0; flex-wrap: wrap; }
      .task-drawer-footer { padding: .75rem; }
      .task-drawer-footer button { width: 100%; }
    }

    @media (max-width: 932px) and (orientation: landscape) and (max-height: 520px) {
      .jira-drawer { width: min(680px, 86vw) !important; }
      .task-drawer-header, .task-drawer-footer { padding-block: .5rem; }
      .task-drawer-body { gap: 1rem; padding: .75rem 1rem; }
    }
  `]
})
export class TaskDetailDrawerComponent implements OnDestroy {
  readonly currentTask = signal<WorkItem | null>(null);
  @Input() set task(val: WorkItem | null) {
    this._task = val;
    this.currentTask.set(val);
    this.localComments.set(val?.comments ? [...val.comments] : []);
    this.newCriterionText.set('');
    this.newTagText.set('');
    this.isTagEditorOpen.set(false);
  }
  get task(): WorkItem | null {
    return this._task;
  }
  private _task: WorkItem | null = null;
  readonly localComments = signal<any[]>([]);

  readonly isTagEditorOpen = signal(false);
  readonly newTagText = signal('');
  readonly newCriterionText = signal('');
  readonly isCriterionBusy = signal(false);

  @Output() dismissed = new EventEmitter<void>();

  readonly projectService = inject(ProjectManagementService);
  private readonly identityService = inject(IdentityService);
  private readonly toastService = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly aiApi = inject(AiBreakdownApi, { optional: true });
  private readonly realtimeService = inject(RealtimeService, { optional: true });
  private readonly cdr = inject(ChangeDetectorRef);

  readonly windowWidth = signal<number>(typeof window !== 'undefined' ? window.innerWidth : 1200);
  readonly isNarrowScreen = computed(() => this.windowWidth() < 860);
  private readonly resizeListener = () => {
    if (typeof window !== 'undefined') {
      this.windowWidth.set(window.innerWidth);
    }
  };

  readonly drawerWidth = signal<number>(640);
  private isResizing = false;

  readonly currentUserInitial = computed(() => {
    const name = this.identityService.authState().currentUser?.displayName;
    return name ? name.charAt(0).toUpperCase() : 'U';
  });
  readonly currentUserAvatar = computed(() => resolveAvatarUrl(this.identityService.authState().currentUser?.avatarUrl));

  readonly epics = this.projectService.epics;
  newCommentText = '';
  readonly isUploadingAttachment = signal(false);
  readonly showMentionDropdown = signal(false);
  readonly mentionQuery = signal('');

  readonly mentionCandidates = computed(() => {
    const q = this.mentionQuery().trim().toLowerCase();
    const members = this.projectMembers();
    if (!q) return members.slice(0, 5);
    return members.filter(m =>
      (m.displayName && m.displayName.toLowerCase().includes(q)) ||
      (m.email && m.email.toLowerCase().includes(q))
    ).slice(0, 5);
  });

  readonly sortedComments = computed(() => {
    const list = this.localComments();
    return [...list].sort((a, b) => (b.createdAt || '').localeCompare(a.createdAt || ''));
  });

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('resize', this.resizeListener);
    }

    this.realtimeService?.on<{ issueId: string; comment: any; workItem: WorkItem }>('WorkItemCommentAdded', payload => {
      if (!payload || !this.task) return;
      const matchId = String(payload.issueId || (payload as any).IssueId || payload.workItem?.id || '').toLowerCase();
      const currentId = String(this.task.id || '').toLowerCase();
      if (!matchId || !currentId || matchId !== currentId) return;

      const rawComments = payload.workItem?.comments || (payload.workItem as any)?.Comments;
      if (Array.isArray(rawComments)) {
        this.task.comments = [...rawComments];
        this.localComments.set([...rawComments]);
      } else if (payload.comment) {
        const c = payload.comment;
        const newCommentObj = {
          id: c.id ?? c.Id,
          authorName: c.authorName ?? c.AuthorName ?? 'Thành viên',
          authorAvatar: c.authorAvatar ?? c.AuthorAvatar,
          content: c.content ?? c.Content ?? '',
          createdAt: c.createdAt ?? c.CreatedAt ?? new Date().toISOString()
        };
        this.localComments.update(list => {
          if (list.some(item => String(item.id).toLowerCase() === String(newCommentObj.id).toLowerCase())) return list;
          return [newCommentObj, ...list];
        });
        if (this.task) {
          this.task.comments = this.localComments();
        }
      }
      this.cdr.markForCheck();
    });
  }

  ngOnDestroy(): void {
    if (typeof window !== 'undefined') {
      window.removeEventListener('resize', this.resizeListener);
    }
  }

  readonly isSubtaskComposerOpen = signal(false);
  readonly newSubtaskTitle = signal('');

  // AI 1 & AI 2 State Signals
  readonly showAiBreakdownPanel = signal(false);
  readonly showAiAssignmentPanel = signal(false);
  readonly currentGenerationId = signal<string | null>(null);

  readonly aiSubtaskSuggestions = signal<Array<{ title: string; points: number; selected: boolean }>>([]);
  readonly aiAcceptanceCriteria = signal<string[]>([]);

  readonly candidateScores = signal<Array<{ name: string; totalScore: number; workloadScore: number; skillScore: number; historyScore: number; capacityScore: number; reason: string }>>([]);

  readonly isBlockModalOpen = signal(false);
  readonly blockReasonInput = signal('');

  readonly projectMembers = computed(() => this.projectService.currentProject()?.members ?? []);
  readonly availableIssueTypes = computed(() => {
    const custom = this.projectService.customIssueTypes().map(t => t.name);
    return custom.length > 0 ? custom : ['Story', 'Task', 'Bug', 'Epic', 'Sub-task'];
  });
  readonly availableStatusNames = computed(() => {
    const dynamic = this.projectService.projectStatuses().map(s => s.name);
    return dynamic.length > 0 ? dynamic : ['To Do', 'In Progress', 'Code Review', 'Done'];
  });
  readonly availablePriorities = computed(() => {
    const fromService = this.projectService.projectPriorities();
    return fromService.length > 0 ? fromService : [
      { id: '1', name: 'Highest', level: 1, colorHex: '#CD1317' },
      { id: '2', name: 'High', level: 2, colorHex: '#E9494B' },
      { id: '3', name: 'Medium', level: 3, colorHex: '#E97F33' },
      { id: '4', name: 'Low', level: 4, colorHex: '#2A8735' },
      { id: '5', name: 'Lowest', level: 5, colorHex: '#57A55A' }
    ];
  });

  readonly parentTask = computed(() => {
    const parentId = this.currentTask()?.parentId;
    if (!parentId) return null;
    return this.projectService.workItems().find(w => w.id === parentId) ?? null;
  });

  readonly availableSprints = computed(() => {
    const curSprintId = this.currentTask()?.sprintId;
    return this.projectService.sprints().filter(s => s.status !== 'completed' || s.id === curSprintId);
  });

  readonly priorityOptions = computed(() => {
    const seen = new Set<string>();
    const options: Array<{ value: string; label: string; colorHex?: string }> = [];
    for (const p of this.availablePriorities()) {
      const value = p.name === 'Highest' ? 'Urgent' : p.name;
      if (seen.has(value)) continue;
      seen.add(value);
      options.push({ value, label: value, colorHex: p.colorHex });
    }
    return options;
  });

  readonly unlinkedCitations = computed(() => {
    const t = this.currentTask();
    return filterUnlinkedCitations(t?.documentCitations, t?.specLinks);
  });


  getAssigneeAvatar(name?: string): string | null {
    return this.projectService.getMemberAvatar(name, this.task?.assigneeAvatar);
  }

  getMemberAvatar(m: ProjectMember): string | null {
    if (m?.avatarUrl) return resolveAvatarUrl(m.avatarUrl);
    return this.projectService.getMemberAvatar(m?.userId || m?.displayName);
  }

  getIssueTypeIcon(item: WorkItem): string {
    const typeName = item.issueType || '';
    const custom = this.projectService.customIssueTypes().find(t =>
      (item.issueTypeId && t.id === item.issueTypeId) ||
      t.name.toLowerCase() === typeName.toLowerCase() ||
      ((typeName.toLowerCase() === 'sub-task' || typeName.toLowerCase() === 'subtask') && (t.isSubtask || t.name.toLowerCase() === 'subtask' || t.name.toLowerCase() === 'sub-task'))
    );
    if (custom?.icon && custom.icon !== 'task') return custom.icon;
    if (item.issueTypeIcon && item.issueTypeIcon !== 'task') return item.issueTypeIcon;
    const map: Record<string, string> = {
      'story': 'bookmark',
      'task': 'task_alt',
      'bug': 'bug_report',
      'epic': 'bolt',
      'sub-task': 'subdirectory_arrow_right',
      'subtask': 'subdirectory_arrow_right'
    };
    return map[typeName.toLowerCase()] || 'task_alt';
  }

  getIssueTypeColor(item: WorkItem): string {
    const typeName = item.issueType || '';
    const custom = this.projectService.customIssueTypes().find(t =>
      (item.issueTypeId && t.id === item.issueTypeId) ||
      t.name.toLowerCase() === typeName.toLowerCase() ||
      ((typeName.toLowerCase() === 'sub-task' || typeName.toLowerCase() === 'subtask') && (t.isSubtask || t.name.toLowerCase() === 'subtask' || t.name.toLowerCase() === 'sub-task'))
    );
    if (custom?.color) return custom.color;
    if (item.issueTypeColor) return item.issueTypeColor;
    const map: Record<string, string> = {
      'story': '#65BA43',
      'task': '#4BADE8',
      'bug': '#E5493A',
      'epic': '#904EE2',
      'sub-task': '#4BADE8',
      'subtask': '#4BADE8'
    };
    return map[typeName.toLowerCase()] || '#4BADE8';
  }

  getAuthorAvatar(name?: string, authorAvatar?: string): string | null {
    return this.projectService.getMemberAvatar(name, authorAvatar);
  }

  getInitials(name?: string): string {
    if (!name) return '?';
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  openBlockTaskModal(): void {
    this.blockReasonInput.set('');
    this.isBlockModalOpen.set(true);
  }

  closeBlockTaskModal(): void {
    this.isBlockModalOpen.set(false);
  }

  submitBlockTask(): void {
    if (!this.task || !this.blockReasonInput().trim()) return;
    const reason = this.blockReasonInput().trim();
    this.projectService.blockIssue(this.task.id, reason).subscribe({
      next: () => {
        if (this.task) {
          this.task.isBlocked = true;
          this.task.blockReason = reason;
        }
        this.toastService.warning('Báo cản trở', 'Công việc đã được đánh dấu cản trở (Blocker).');
        this.closeBlockTaskModal();
      },
      error: () => {
        this.toastService.error('Báo cản trở', 'Không thể đánh dấu cản trở.');
      }
    });
  }

  unblockTask(): void {
    if (!this.task) return;
    this.projectService.unblockIssue(this.task.id).subscribe({
      next: () => {
        if (this.task) {
          this.task.isBlocked = false;
          this.task.blockReason = undefined;
        }
        this.toastService.success('Gỡ cản trở', 'Đã gỡ bỏ cản trở cho công việc.');
      },
      error: () => {
        this.toastService.error('Gỡ cản trở', 'Không thể gỡ cản trở.');
      }
    });
  }

  toggleAiBreakdownPanel(): void {
    this.showAiAssignmentPanel.set(false);
    const opening = !this.showAiBreakdownPanel();
    this.showAiBreakdownPanel.set(opening);
    if (opening && this.task?.id && this.aiApi) {
      this.aiApi.generate(this.task.id, this.task.description).subscribe({
        next: res => {
          if (res.generationId) {
            this.currentGenerationId.set(res.generationId);
          }
          if (res.suggestedSubTasks && res.suggestedSubTasks.length > 0) {
            this.aiSubtaskSuggestions.set(
              res.suggestedSubTasks.map(t => ({
                title: t.title ?? 'Sub-task',
                points: Number(t.estimatePoints ?? 2),
                selected: true
              }))
            );
          }
          if (res.acceptanceCriteria && res.acceptanceCriteria.length > 0) {
            this.aiAcceptanceCriteria.set(res.acceptanceCriteria);
          } else {
            this.aiAcceptanceCriteria.set([]);
          }
        },
        error: () => undefined
      });
    }
  }

  toggleAiAssignmentPanel(): void {
    this.showAiBreakdownPanel.set(false);
    const opening = !this.showAiAssignmentPanel();
    this.showAiAssignmentPanel.set(opening);
    if (opening) {
      const members = this.projectMembers();
      const allWorkItems = this.projectService.workItems();
      const scores = members.map(m => {
        const activeTasks = allWorkItems.filter(w => (w.assigneeName === m.displayName || w.assigneeId === m.userId) && w.statusName !== 'Done').length;
        const workloadScore = Math.max(30, Math.min(100, 100 - activeTasks * 15));
        const skillScore = m.role === 'Developer' ? 90 : m.role === 'QA Engineer' ? 85 : 80;
        const historyScore = 85;
        const capacityScore = Math.max(40, 100 - activeTasks * 10);
        const totalScore = Math.round(workloadScore * 0.4 + skillScore * 0.3 + historyScore * 0.2 + capacityScore * 0.1);
        return {
          name: m.displayName,
          totalScore,
          workloadScore,
          skillScore,
          historyScore,
          capacityScore,
          reason: `Đang phụ trách ${activeTasks} công việc còn mở. Vai trò ${m.role}.`
        };
      }).sort((a, b) => b.totalScore - a.totalScore);
      this.candidateScores.set(scores);
    }
  }

  applyAiBreakdown(): void {
    if (!this.task) return;
    const selectedSubs = this.aiSubtaskSuggestions().filter(s => s.selected);
    const genId = this.currentGenerationId();
    if (genId && this.aiApi) {
      this.aiApi.apply(
        genId,
        selectedSubs.map(s => ({ title: s.title, estimatePoints: s.points }))
      ).subscribe({
        error: () => undefined
      });
    }
    selectedSubs.forEach(s => {
      this.projectService.createSubTask(this.task!.id, s.title);
    });
    this.toastService.success('Đã Áp Dụng AI Breakdown', `Đã tự động tạo ${selectedSubs.length} Sub-task vào Issue [${this.task.issueKey}]`);
    this.showAiBreakdownPanel.set(false);
  }

  selectCandidate(candidateName: string): void {
    if (!this.task) return;
    this.task.assigneeName = candidateName;
    this.onFieldChange();
    this.toastService.success('Đồng Ý Đề Xuất AI 2', `Đã chọn người xử lý: ${candidateName}`);
    this.showAiAssignmentPanel.set(false);
  }

  startResizing(event: MouseEvent): void {
    if (this.isNarrowScreen()) return;
    event.preventDefault();
    this.isResizing = true;
    const onMouseMove = (moveEvent: MouseEvent) => {
      if (!this.isResizing) return;
      const newWidth = window.innerWidth - moveEvent.clientX;
      if (newWidth >= 380 && newWidth <= window.innerWidth * 0.85) {
        this.drawerWidth.set(newWidth);
      }
    };
    const onMouseUp = () => {
      this.isResizing = false;
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };
    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  }

  get drawerSubtasks(): WorkItem[] {
    if (!this.task) return [];
    return this.projectService.workItems().filter(w => w.parentId === this.task?.id);
  }

  openRelatedTask(item: WorkItem): void {
    this.task = item;
    this.projectService.activeDrawerTask.set(item);
    this.cdr.markForCheck();
  }

  hasEpicName(name: string): boolean {
    return this.epics().some(e => e.name === name);
  }

  hasPriorityOption(priority: string): boolean {
    return this.priorityOptions().some(p => p.value === priority);
  }

  extractCiteDocName(cite: string): string {
    return extractCiteDocName(cite);
  }

  onSprintChange(sprintId: string): void {
    if (!this.task) return;
    const cleanId = sprintId || '';
    this.task.sprintId = cleanId;
    this.task.sprintName = cleanId
      ? (this.projectService.sprints().find(s => s.id === cleanId)?.name ?? '')
      : 'Backlog';
    this.onFieldChange();
  }

  addLabelFromDrawer(): void {
    if (!this.task) return;
    const clean = this.newTagText().trim().replace(/^#+/, '');
    if (!clean) return;
    const current = [...(this.task.labels ?? [])];
    if (!current.some(l => l.toLowerCase() === clean.toLowerCase())) {
      current.push(clean);
      this.task.labels = current;
      this.onFieldChange();
    }
    this.newTagText.set('');
  }

  removeLabelFromDrawer(lbl: string): void {
    if (!this.task) return;
    this.task.labels = (this.task.labels ?? []).filter(l => l !== lbl);
    this.onFieldChange();
  }

  addCriterionFromDrawer(): void {
    if (!this.task || this.isCriterionBusy()) return;
    const content = this.newCriterionText().trim();
    if (!content) return;
    const taskId = this.task.id;
    this.isCriterionBusy.set(true);
    this.projectService.addCriterion(taskId, content).subscribe({
      next: () => {
        this.newCriterionText.set('');
        this.projectService.refreshWorkItem(taskId).subscribe({
          next: updated => {
            this.syncRefreshedTask(updated);
            this.isCriterionBusy.set(false);
          },
          error: () => this.isCriterionBusy.set(false)
        });
      },
      error: () => {
        this.isCriterionBusy.set(false);
        this.toastService.error('Tiêu chí nghiệm thu', 'Không thể thêm tiêu chí nghiệm thu.');
      }
    });
  }

  toggleCriterion(criterion: { id: string; content: string; isMet: boolean }): void {
    if (!this.task || this.isCriterionBusy()) return;
    const taskId = this.task.id;
    const nextMet = !criterion.isMet;
    criterion.isMet = nextMet;
    this.isCriterionBusy.set(true);
    this.projectService.updateCriterion(criterion.id, criterion.content, nextMet).subscribe({
      next: () => {
        this.projectService.refreshWorkItem(taskId).subscribe({
          next: updated => {
            this.syncRefreshedTask(updated);
            this.isCriterionBusy.set(false);
          },
          error: () => this.isCriterionBusy.set(false)
        });
      },
      error: () => {
        criterion.isMet = !nextMet;
        this.isCriterionBusy.set(false);
        this.toastService.error('Tiêu chí nghiệm thu', 'Không thể cập nhật trạng thái tiêu chí.');
      }
    });
  }

  updateCriterionText(criterion: { id: string; content: string; isMet: boolean }, nextValue: string): void {
    if (!this.task || this.isCriterionBusy()) return;
    const trimmed = nextValue.trim();
    if (!trimmed || trimmed === criterion.content) return;
    const taskId = this.task.id;
    this.isCriterionBusy.set(true);
    this.projectService.updateCriterion(criterion.id, trimmed, criterion.isMet).subscribe({
      next: () => {
        criterion.content = trimmed;
        this.projectService.refreshWorkItem(taskId).subscribe({
          next: updated => {
            this.syncRefreshedTask(updated);
            this.isCriterionBusy.set(false);
          },
          error: () => this.isCriterionBusy.set(false)
        });
      },
      error: () => {
        this.isCriterionBusy.set(false);
        this.toastService.error('Tiêu chí nghiệm thu', 'Không thể cập nhật nội dung tiêu chí.');
      }
    });
  }

  deleteCriterionFromDrawer(criterionId: string): void {
    if (!this.task || this.isCriterionBusy()) return;
    const taskId = this.task.id;
    this.isCriterionBusy.set(true);
    this.projectService.deleteCriterion(criterionId).subscribe({
      next: () => {
        this.projectService.refreshWorkItem(taskId).subscribe({
          next: updated => {
            this.syncRefreshedTask(updated);
            this.isCriterionBusy.set(false);
          },
          error: () => this.isCriterionBusy.set(false)
        });
      },
      error: () => {
        this.isCriterionBusy.set(false);
        this.toastService.error('Tiêu chí nghiệm thu', 'Không thể xóa tiêu chí nghiệm thu.');
      }
    });
  }

  private syncRefreshedTask(updated: WorkItem): void {
    if (!updated) return;
    if (this.task && String(this.task.id).toLowerCase() === String(updated.id).toLowerCase()) {
      this._task = updated;
      this.currentTask.set(updated);
      this.localComments.set(updated.comments ? [...updated.comments] : []);
      this.cdr.markForCheck();
    }
  }

  onFieldChange(): void {
    if (this.task) {
      if (this.task.assigneeName) {
        const m = this.projectMembers().find(x => x.displayName === this.task?.assigneeName);
        if (m) {
          this.task.assigneeId = m.userId;
          this.task.assigneeAvatar = m.avatarUrl;
        }
      } else {
        this.task.assigneeName = '';
        this.task.assigneeId = undefined;
        this.task.assigneeAvatar = undefined;
      }
      if (this.task.epicName) {
        const e = this.epics().find(x => x.name === this.task?.epicName);
        if (e) this.task.epicId = e.id;
      } else {
        this.task.epicName = '';
        this.task.epicId = undefined;
      }
      this.projectService.updateWorkItem(this.task);
    }
  }

  addSubtaskFromDrawer(): void {
    if (!this.task) return;
    const title = this.newSubtaskTitle().trim();
    if (!title) return;
    this.projectService.createSubTask(this.task.id, title);
    this.toastService.success('Công việc con', 'Đã thêm công việc con.');
    this.cancelSubtaskComposer();
  }

  cancelSubtaskComposer(): void {
    this.newSubtaskTitle.set('');
    this.isSubtaskComposerOpen.set(false);
  }

  cloneTask(): void {
    if (!this.task) return;
    const cloned = this.projectService.cloneWorkItem(this.task.id);
    if (cloned) {
      this.toastService.success('Nhân bản', `Đã tạo bản sao cho [${this.task.issueKey}].`);
    }
  }

  deleteTask(): void {
    if (!this.task) return;
    const taskId = this.task.id;
    const issueKey = this.task.issueKey;
    this.confirmDialog.confirm({
      title: 'Xóa công việc',
      message: `Bạn có chắc chắn muốn xóa công việc [${issueKey}]? Hành động này không thể hoàn tác.`,
      type: 'danger',
      confirmText: 'Xóa công việc',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.projectService.deleteWorkItem(taskId);
        this.toastService.info('Đang xóa công việc', 'Vui lòng chờ xác nhận từ máy chủ.');
        this.dismissed.emit();
      }
    });
  }

  onCommentInputChange(val: string): void {
    this.newCommentText = val;
    const match = val.match(/@([a-zA-Z0-9_\u00C0-\u024F\u1EA0-\u1EF9]*)$/);
    if (match) {
      this.mentionQuery.set(match[1]);
      this.showMentionDropdown.set(true);
    } else {
      this.showMentionDropdown.set(false);
    }
  }

  insertMention(candidate: { displayName: string }): void {
    const val = this.newCommentText;
    const replaced = val.replace(/@([a-zA-Z0-9_\u00C0-\u024F\u1EA0-\u1EF9]*)$/, `@${candidate.displayName} `);
    this.newCommentText = replaced;
    this.showMentionDropdown.set(false);
  }

  isVideoAttachment(fileName?: string): boolean {
    if (!fileName) return false;
    const ext = fileName.toLowerCase();
    return ext.endsWith('.mp4') || ext.endsWith('.mov') || ext.endsWith('.webm') || ext.endsWith('.avi');
  }

  getAttachmentDownloadUrl(attachmentId: string): string {
    return this.projectService.getDownloadUrl ? `${this.projectService.getDownloadUrl(attachmentId).replace('/documents/', '/attachments/')}` : `/api/attachments/${attachmentId}`;
  }

  formatFileSize(bytes?: number): string {
    if (!bytes || bytes <= 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }

  triggerAttachmentUpload(): void {
    const input = document.getElementById('task-attachment-input') as HTMLInputElement;
    if (input) {
      input.value = '';
      input.click();
    }
  }

  onAttachmentSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0 || !this.task) return;
    const file = input.files[0];
    if (file.size > 50 * 1024 * 1024) {
      this.toastService.error('Tệp đính kèm', 'Kích thước tệp vượt quá giới hạn 50MB.');
      return;
    }
    this.isUploadingAttachment.set(true);
    this.projectService.uploadAttachment(this.task.id, file).subscribe({
      next: () => {
        this.isUploadingAttachment.set(false);
        this.toastService.success('Đính kèm', `Đã tải lên tệp "${file.name}".`);
        this.projectService.getIssue(this.task!.id).subscribe({
          next: updated => {
            if (this.task && this.task.id === updated.id) {
              this.task.attachments = updated.attachments;
            }
          }
        });
      },
      error: (err: any) => {
        this.isUploadingAttachment.set(false);
        const errorMsg = err?.error?.error || err?.error?.detail || err?.error?.message || err?.message || 'Lỗi khi tải tệp đính kèm lên máy chủ.';
        this.toastService.error('Đính kèm', errorMsg);
      }
    });
  }

  submitComment(): void {
    const text = this.newCommentText.trim();
    if (this.task && text) {
      const mentionedUserIds: string[] = [];
      const members = this.projectMembers();
      for (const m of members) {
        if (m.displayName && text.includes(`@${m.displayName}`)) {
          mentionedUserIds.push(m.userId);
        }
      }

      this.projectService.postComment(this.task.id, text, mentionedUserIds).subscribe({
        next: updated => {
          if (this.task && String(this.task.id).toLowerCase() === String(updated.id).toLowerCase()) {
            this.task.comments = updated.comments ? [...updated.comments] : [];
            this.localComments.set(this.task.comments);
          }
          this.toastService.success('Bình luận', 'Đã đăng bình luận thành công.');
          this.newCommentText = '';
          this.showMentionDropdown.set(false);
          this.cdr.markForCheck();
        },
        error: () => {
          this.toastService.error('Bình luận', 'Không thể gửi bình luận lên máy chủ.');
        }
      });
    }
  }
}
