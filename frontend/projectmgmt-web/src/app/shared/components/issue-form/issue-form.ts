import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, concat, last } from 'rxjs';
import { ISSUE_PRIORITIES, IssueCoreDto } from '../../../core/api/issue-api.models';
import { IssueApi } from '../../../core/api/issue.api';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ProjectManagementService } from '../../../core/services/project-management.service';
import { IssueFormValue, ParentOption, toFormValue, toUpdateRequest, validateIssueForm } from './issue-form.model';

/**
 * Modal sửa Issue dùng API module DeliveryIntelligence (PATCH /issues/{id}, PUT /issues/{id}/parent).
 * Dùng lại được ở Issue Detail, Backlog và Board: chỉ cần truyền issueId.
 */
@Component({
  selector: 'app-issue-form',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="responsive-modal-backdrop fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50" role="dialog" aria-modal="true" aria-labelledby="issue-form-title">
      <form class="responsive-modal-panel w-full max-w-2xl max-h-[90vh] overflow-y-auto bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800" (ngSubmit)="save()">
        <header class="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-800">
          <h2 id="issue-form-title" class="text-base font-bold text-slate-900 dark:text-white">Sửa {{ issue()?.issueKey ?? issueLabel() }}</h2>
          <button type="button" class="w-8 h-8 rounded-lg hover:bg-slate-200 dark:hover:bg-slate-800 text-slate-500 flex items-center justify-center cursor-pointer" (click)="dismissed.emit()" aria-label="Đóng"><span class="material-symbols-outlined text-[18px]">close</span></button>
        </header>

        @if (loading()) {
          <p class="px-6 py-8 text-sm text-slate-500" role="status">Đang tải Issue…</p>
        } @else if (form(); as value) {
          <div class="p-6 flex flex-col gap-4 text-sm">
            <label class="flex flex-col gap-1.5 font-semibold text-slate-600">Tiêu đề *
              <input name="title" [(ngModel)]="value.title" required maxlength="500" class="h-10 px-3 rounded-xl border border-slate-200 font-medium text-slate-900" />
            </label>
            <label class="flex flex-col gap-1.5 font-semibold text-slate-600">Mô tả
              <textarea name="description" [(ngModel)]="value.description" rows="5" class="px-3 py-2 rounded-xl border border-slate-200 font-normal text-slate-900"></textarea>
            </label>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <label class="flex flex-col gap-1.5 font-semibold text-slate-600">Người xử lý
                <select name="assignee" [(ngModel)]="value.assigneeId" class="h-10 px-3 rounded-xl border border-slate-200 text-slate-900">
                  <option value="">Chưa phân công</option>
                  @for (member of members(); track member.userId) { <option [value]="member.userId">{{ member.displayName }}</option> }
                </select>
              </label>
              <label class="flex flex-col gap-1.5 font-semibold text-slate-600">Ưu tiên
                <select name="priority" [(ngModel)]="value.priorityId" class="h-10 px-3 rounded-xl border border-slate-200 text-slate-900">
                  <option value="">Không đặt</option>
                  @for (priority of priorities; track priority.id) { <option [value]="priority.id">{{ priority.name }}</option> }
                </select>
              </label>
              <label class="flex flex-col gap-1.5 font-semibold text-slate-600">Story points
                <input name="points" type="number" min="0" max="9999.99" step="0.5" [(ngModel)]="value.storyPoints" class="h-10 px-3 rounded-xl border border-slate-200 text-slate-900" />
              </label>
              <label class="flex flex-col gap-1.5 font-semibold text-slate-600">Ước lượng (giờ)
                <input name="estimate" type="number" min="0" step="0.5" [(ngModel)]="value.estimateHours" class="h-10 px-3 rounded-xl border border-slate-200 text-slate-900" />
              </label>
              <label class="flex flex-col gap-1.5 font-semibold text-slate-600">Hạn hoàn thành
                <input name="dueDate" type="date" [(ngModel)]="value.dueDate" class="h-10 px-3 rounded-xl border border-slate-200 text-slate-900" />
              </label>
              @if (parentOptions().length || value.parentId) {
                <label class="flex flex-col gap-1.5 font-semibold text-slate-600">Công việc cha
                  <select name="parent" [(ngModel)]="value.parentId" class="h-10 px-3 rounded-xl border border-slate-200 text-slate-900">
                    @if (!isSubtask()) { <option value="">Không có</option> }
                    @for (option of parentOptions(); track option.id) { <option [value]="option.id">{{ option.label }}</option> }
                  </select>
                </label>
              }
            </div>
            @if (error()) { <p role="alert" class="text-red-700 font-medium">{{ error() }}</p> }
          </div>
          <footer class="flex justify-end gap-2 px-6 py-4 border-t border-slate-200 dark:border-slate-800">
            <button type="button" class="px-4 h-10 rounded-xl border border-slate-300 font-semibold" (click)="dismissed.emit()" [disabled]="saving()">Hủy</button>
            <button type="submit" class="px-4 h-10 rounded-xl bg-primary text-white font-bold disabled:opacity-50" [disabled]="saving() || !value.title.trim()">{{ saving() ? 'Đang lưu…' : 'Lưu thay đổi' }}</button>
          </footer>
        } @else {
          <p role="alert" class="px-6 py-8 text-sm text-red-700">{{ error() }}</p>
        }
      </form>
    </div>
  `
})
export class IssueFormComponent implements OnInit {
  readonly issueId = input.required<string>();
  readonly saved = output<IssueCoreDto>();
  readonly dismissed = output<void>();

  private readonly api = inject(IssueApi);
  private readonly projects = inject(ProjectManagementService);

  readonly priorities = ISSUE_PRIORITIES;
  readonly issue = signal<IssueCoreDto | null>(null);
  readonly form = signal<IssueFormValue | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly members = computed(() => this.projects.currentProject().members ?? []);

  private readonly workItem = computed(() => this.projects.workItems().find(item => item.id === this.issueId()));
  readonly issueLabel = computed(() => this.workItem()?.issueKey ?? 'Issue');
  readonly isSubtask = computed(() => this.workItem()?.issueType === 'Sub-task');

  /** Epic chỉ nhận Story/Task/Bug; Sub-task chỉ nhận cha không phải Epic/Sub-task. Backend vẫn kiểm lại luật. */
  readonly parentOptions = computed<ParentOption[]>(() => {
    const current = this.workItem();
    if (!current || current.issueType === 'Epic') return [];
    return this.projects.workItems()
      .filter(item => item.id !== current.id && item.projectId === current.projectId && item.parentId !== current.id)
      .filter(item => this.isSubtask() ? item.issueType !== 'Epic' && item.issueType !== 'Sub-task' : item.issueType === 'Epic')
      .map(item => ({ id: item.id, label: `${item.issueKey} · ${item.title}` }));
  });

  ngOnInit(): void {
    this.api.getCore(this.issueId()).subscribe({
      next: issue => { this.issue.set(issue); this.form.set(toFormValue(issue)); this.loading.set(false); },
      error: e => { this.error.set(apiErrorMessage(e, 'Không tải được Issue.')); this.loading.set(false); }
    });
  }

  save(): void {
    const value = this.form();
    const issue = this.issue();
    if (!value || !issue || this.saving()) return;
    const invalid = validateIssueForm(value);
    if (invalid) { this.error.set(invalid); return; }

    const requests: Observable<IssueCoreDto>[] = [this.api.update(issue.id, toUpdateRequest(value))];
    if ((value.parentId || null) !== (issue.parentId ?? null)) {
      requests.push(this.api.changeParent(issue.id, value.parentId || null));
    }

    this.saving.set(true);
    this.error.set('');
    concat(...requests).pipe(last()).subscribe({
      next: updated => { this.saving.set(false); this.saved.emit(updated); },
      error: e => { this.saving.set(false); this.error.set(apiErrorMessage(e, 'Không lưu được Issue.')); }
    });
  }
}
