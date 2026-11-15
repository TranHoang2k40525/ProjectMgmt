import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AiQuickActionService } from '../../../core/services/ai-quick-action.service';

@Component({
  selector: 'app-ai-quick-action-tray',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (quickAi.isOpen()) {
      <section
        class="fixed bottom-4 right-4 z-[60] w-[min(470px,calc(100vw-24px))] max-h-[calc(100vh-88px)] flex flex-col rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 shadow-2xl overflow-hidden animate-fade-in"
        role="dialog"
        aria-modal="false"
        aria-label="Xác nhận đề xuất Trợ Lý AI"
      >
        <!-- Header -->
        <header class="flex items-center justify-between gap-2 px-4 py-3 bg-slate-50/90 dark:bg-slate-800/80 border-b border-slate-200 dark:border-slate-700">
          <div class="flex items-center gap-2.5 min-w-0">
            <div class="w-8 h-8 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 border border-emerald-200 dark:border-emerald-800 flex items-center justify-center shrink-0">
              <span class="material-symbols-outlined text-[18px] text-emerald-700 dark:text-emerald-300" aria-hidden="true">
                {{ quickAi.actionType() === 'assignee_suggestion' ? 'person_check' : 'auto_awesome' }}
              </span>
            </div>
            <div class="min-w-0">
              <div class="flex items-center gap-1.5 flex-wrap">
                <h3 class="text-xs font-bold text-slate-900 dark:text-white truncate">
                  {{ quickAi.actionType() === 'assignee_suggestion' ? 'Trợ Lý AI · Đề xuất phân công' : 'Trợ Lý AI · Phân rã Sub-task' }}
                </h3>
                @if (quickAi.targetItem(); as item) {
                  <span class="px-1.5 py-0.5 rounded bg-slate-200/80 dark:bg-slate-700 text-slate-800 dark:text-slate-100 font-mono text-[10px] font-semibold">
                    {{ item.issueKey }}
                  </span>
                }
              </div>
              <p class="text-[11px] text-slate-500 dark:text-slate-400 truncate">
                {{ quickAi.targetItem()?.title }}
              </p>
            </div>
          </div>

          <div class="flex items-center gap-1.5 shrink-0">
            @if (quickAi.phase() === 'staged' || quickAi.phase() === 'applying') {
              <span class="hidden sm:inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-amber-50 dark:bg-amber-950/50 border border-amber-200 dark:border-amber-800 text-[10px] font-semibold text-amber-800 dark:text-amber-300">
                <span class="material-symbols-outlined text-[12px]" aria-hidden="true">memory</span>
                <span>Redis staged</span>
              </span>
            }
            <button
              type="button"
              (click)="quickAi.cancelAndDismiss()"
              title="Hủy và đóng"
              class="w-7 h-7 rounded-lg flex items-center justify-center text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-200/60 dark:hover:bg-slate-700 transition-colors cursor-pointer"
            >
              <span class="material-symbols-outlined text-[17px]" aria-hidden="true">close</span>
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="flex-1 overflow-y-auto px-4 py-3 space-y-3 text-xs">
          @if (quickAi.phase() === 'running') {
            <div class="space-y-2.5 py-1">
              <div class="flex items-center justify-between text-[11px]">
                <span class="font-medium text-slate-700 dark:text-slate-200 flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[15px] text-emerald-600 animate-spin" aria-hidden="true">sync</span>
                  <span>{{ quickAi.progressLabel() }}</span>
                </span>
                <span class="font-mono text-slate-500 shrink-0">{{ quickAi.elapsedSeconds() }}s</span>
              </div>
              <div class="w-full h-1.5 rounded-full bg-slate-100 dark:bg-slate-800 overflow-hidden">
                <div
                  class="h-full bg-emerald-600 transition-all duration-300 rounded-full"
                  [style.width.%]="quickAi.progressPercent()"
                ></div>
              </div>
              @if (quickAi.traceItems().length > 0) {
                <div class="space-y-1 pt-1">
                  @for (trace of quickAi.traceItems(); track $index) {
                    <div class="flex items-center justify-between gap-2 px-2.5 py-1.5 rounded-lg bg-slate-50 dark:bg-slate-800/60 border border-slate-200/70 dark:border-slate-700/60 text-[11px]">
                      <span class="flex items-center gap-1.5 min-w-0 text-slate-700 dark:text-slate-300">
                        <span class="material-symbols-outlined text-[13px] text-emerald-600 shrink-0" aria-hidden="true">check_circle</span>
                        <span class="truncate">{{ trace.summaryVi }}</span>
                      </span>
                      @if (trace.durationMs !== undefined && trace.durationMs !== null) {
                        <span class="font-mono text-[10px] text-slate-400 shrink-0">{{ trace.durationMs }}ms</span>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          }

          @if (quickAi.phase() === 'error') {
            <div class="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-rose-800 dark:text-rose-200 flex items-start gap-2">
              <span class="material-symbols-outlined text-[18px] shrink-0 mt-0.5" aria-hidden="true">error</span>
              <div class="space-y-1 min-w-0">
                <p class="font-semibold">Không thể hoàn tất đề xuất AI</p>
                <p class="text-[11px] leading-relaxed">{{ quickAi.errorMessage() }}</p>
              </div>
            </div>
          }

          @if ((quickAi.phase() === 'staged' || quickAi.phase() === 'applying') && quickAi.plan(); as plan) {
            @if (quickAi.errorMessage(); as errMsg) {
              <div class="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-rose-800 dark:text-rose-200 flex items-start gap-2">
                <span class="material-symbols-outlined text-[16px] shrink-0 mt-0.5" aria-hidden="true">error</span>
                <span class="text-[11px]">{{ errMsg }}</span>
              </div>
            }

            <!-- Summary / Rationale -->
            <div class="p-2.5 rounded-xl bg-emerald-50/70 dark:bg-emerald-950/30 border border-emerald-200/80 dark:border-emerald-800/70 space-y-1">
              <div class="flex items-center justify-between gap-2 text-[11px] font-semibold text-emerald-900 dark:text-emerald-200">
                <span>{{ plan.summary }}</span>
                <span class="font-mono text-[10px] px-1.5 py-0.5 rounded bg-white/80 dark:bg-slate-800 border border-emerald-200 dark:border-emerald-700 shrink-0">
                  {{ plan.sprintName }}
                </span>
              </div>
              <p class="text-[11px] text-emerald-800/90 dark:text-emerald-300/90 leading-relaxed">{{ plan.rationale }}</p>
            </div>

            <!-- Subtasks staged preview -->
            @if (plan.actionType === 'subtask_breakdown') {
              <div class="space-y-2">
                @for (sub of plan.proposedSubtasks; track sub.tempId) {
                  @let checked = quickAi.subtaskSelections()[sub.tempId] !== false;
                  <div
                    class="p-2.5 rounded-xl border transition-colors space-y-1.5"
                    [class.bg-white]="checked"
                    [class.dark:bg-slate-900]="checked"
                    [class.border-slate-200]="checked"
                    [class.dark:border-slate-700]="checked"
                    [class.bg-slate-50]="!checked"
                    [class.opacity-60]="!checked"
                    [class.border-slate-200/60]="!checked"
                  >
                    <div class="flex items-start gap-2">
                      <input
                        type="checkbox"
                        [checked]="checked"
                        (change)="quickAi.toggleSubtask(sub.tempId)"
                        [attr.aria-label]="'Chọn Sub-task ' + sub.title"
                        class="mt-1 rounded border-slate-300 text-emerald-700 focus:ring-emerald-600 cursor-pointer"
                      />
                      <div class="flex-1 min-w-0 space-y-1">
                        <input
                          type="text"
                          [ngModel]="quickAi.subtaskTitles()[sub.tempId]"
                          (ngModelChange)="quickAi.updateSubtaskTitle(sub.tempId, $event)"
                          [disabled]="!checked || quickAi.phase() === 'applying'"
                          class="w-full px-2 py-1 rounded-md bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-xs font-semibold text-slate-900 dark:text-white focus:outline-none focus:border-emerald-600"
                        />
                        <div class="flex items-center flex-wrap gap-1.5 text-[10px]">
                          <span class="px-1.5 py-0.5 rounded bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 font-semibold">
                            {{ sub.role }}
                          </span>
                          <span class="px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 font-mono">
                            {{ sub.storyPoints }} SP · {{ sub.estimatedHours }}h
                          </span>
                          <span class="px-1.5 py-0.5 rounded bg-amber-50 dark:bg-amber-950/40 text-amber-800 dark:text-amber-300 font-medium">
                            {{ sub.priority }}
                          </span>
                          @if (sub.suggestedAssigneeName) {
                            <span class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded bg-emerald-50 dark:bg-emerald-950/50 text-emerald-800 dark:text-emerald-300">
                              <span class="material-symbols-outlined text-[11px]" aria-hidden="true">person</span>
                              <span>{{ sub.suggestedAssigneeName }}</span>
                            </span>
                          }
                        </div>
                      </div>
                    </div>
                  </div>
                }
              </div>
            }

            <!-- Assignee Candidates staged preview -->
            @if (plan.actionType === 'assignee_suggestion') {
              <div class="space-y-2">
                @for (cand of plan.assigneeCandidates; track cand.userId) {
                  @let selected = quickAi.selectedAssigneeId() === cand.userId;
                  <button
                    type="button"
                    (click)="quickAi.selectAssignee(cand.userId)"
                    [disabled]="quickAi.phase() === 'applying'"
                    class="w-full text-left p-2.5 rounded-xl border transition-all cursor-pointer space-y-1.5"
                    [class.border-emerald-600]="selected"
                    [class.bg-emerald-50/50]="selected"
                    [class.dark:bg-emerald-950/30]="selected"
                    [class.border-slate-200]="!selected"
                    [class.dark:border-slate-700]="!selected"
                    [class.hover:border-slate-300]="!selected"
                  >
                    <div class="flex items-center justify-between gap-2">
                      <div class="flex items-center gap-2 min-w-0">
                        <span
                          class="material-symbols-outlined text-[16px] shrink-0"
                          [class.text-emerald-700]="selected"
                          [class.text-slate-400]="!selected"
                          aria-hidden="true"
                        >
                          {{ selected ? 'radio_button_checked' : 'radio_button_unchecked' }}
                        </span>
                        <div class="min-w-0">
                          <div class="flex items-center gap-1.5 flex-wrap">
                            <span class="font-semibold text-slate-900 dark:text-white truncate">{{ cand.displayName }}</span>
                            @if (cand.isRecommended) {
                              <span class="px-1.5 py-0.5 rounded bg-emerald-100 dark:bg-emerald-900/70 text-emerald-800 dark:text-emerald-200 text-[10px] font-bold">
                                #1 AI đề xuất
                              </span>
                            }
                          </div>
                          <p class="text-[10px] text-slate-500 dark:text-slate-400 truncate">
                            {{ cand.jobTitle }} · {{ cand.activeSprintTasksCount }} việc đang làm · Còn {{ cand.remainingAvailableHours }}h rảnh
                          </p>
                        </div>
                      </div>
                      <span class="px-2 py-0.5 rounded-full bg-slate-900 dark:bg-slate-700 text-white font-mono text-[11px] font-bold shrink-0">
                        {{ cand.totalScore | number: '1.0-1' }}đ
                      </span>
                    </div>
                    <p class="text-[11px] text-slate-600 dark:text-slate-300 leading-relaxed pl-6">
                      {{ cand.reason }}
                    </p>
                  </button>
                }

                @if (plan.recommendedUserId && quickAi.selectedAssigneeId() && quickAi.selectedAssigneeId() !== plan.recommendedUserId) {
                  <div class="pt-1">
                    <input
                      type="text"
                      [ngModel]="quickAi.overrideReason()"
                      (ngModelChange)="quickAi.overrideReason.set($event)"
                      placeholder="Lý do chọn ứng viên khác (tùy chọn, giúp AI học chính xác hơn)..."
                      class="w-full px-2.5 py-1.5 rounded-lg bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-[11px] focus:outline-none focus:border-emerald-600"
                    />
                  </div>
                }
              </div>
            }
          }
        </div>

        <!-- Footer -->
        <footer class="flex items-center justify-between gap-2 px-4 py-2.5 bg-slate-50 dark:bg-slate-800/70 border-t border-slate-200 dark:border-slate-700">
          <span class="text-[10px] text-slate-500 dark:text-slate-400">
            @if (quickAi.phase() === 'staged') {
              Dữ liệu đang lưu tạm trên Redis · Chờ bạn xác nhận
            } @else if (quickAi.phase() === 'applying') {
              Đang ghi nhận vào hệ thống...
            } @else if (quickAi.phase() === 'running') {
              Không ghi vào DB trước khi bạn xác nhận
            }
          </span>

          <div class="flex items-center gap-2 shrink-0">
            <button
              type="button"
              (click)="quickAi.cancelAndDismiss()"
              [disabled]="quickAi.phase() === 'applying'"
              class="h-8 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-medium text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 disabled:opacity-50 cursor-pointer"
            >
              Hủy bỏ
            </button>

            @if (quickAi.phase() === 'error') {
              <button
                type="button"
                (click)="quickAi.retry()"
                class="h-8 px-3.5 rounded-lg bg-slate-900 dark:bg-emerald-700 text-white text-xs font-semibold hover:bg-slate-800 cursor-pointer flex items-center gap-1"
              >
                <span class="material-symbols-outlined text-[15px]" aria-hidden="true">refresh</span>
                <span>Thử lại</span>
              </button>
            }

            @if (quickAi.phase() === 'staged' || quickAi.phase() === 'applying') {
              <button
                type="button"
                (click)="quickAi.confirmApply()"
                [disabled]="quickAi.phase() === 'applying'"
                class="h-8 px-3.5 rounded-lg bg-emerald-700 hover:bg-emerald-800 text-white text-xs font-semibold disabled:opacity-60 cursor-pointer flex items-center gap-1.5 shadow-xs"
              >
                <span class="material-symbols-outlined text-[15px]" aria-hidden="true">check</span>
                <span>
                  @if (quickAi.phase() === 'applying') {
                    Đang áp dụng...
                  } @else if (quickAi.actionType() === 'subtask_breakdown') {
                    Xác nhận áp dụng ({{ quickAi.selectedSubtaskCount() }})
                  } @else {
                    Xác nhận áp dụng
                  }
                </span>
              </button>
            }
          </div>
        </footer>
      </section>
    }
  `,
})
export class AiQuickActionTrayComponent {
  readonly quickAi = inject(AiQuickActionService);
}
