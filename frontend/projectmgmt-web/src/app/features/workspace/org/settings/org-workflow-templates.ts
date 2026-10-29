import { DOCUMENT, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { apiErrorMessage } from '../../../../core/http/api-error-message';
import { ConfirmDialogService } from '../../../../core/services/confirm-dialog.service';
import { ToastService } from '../../../../core/services/toast.service';
import { ModalShellComponent } from '../../components/modal-shell';
import { SectionStateComponent } from '../../components/section-state';
import { UI } from '../../components/ui-classes';
import { WorkspaceDataSource } from '../../data/workspace-data-source';
import { ManagementType, STATUS_CATEGORY_LABELS, StatusCategory, WorkflowTemplate } from '../../data/workspace.models';
import { WorkspaceContextService } from '../../workspace-context.service';
import {
  MAX_TEMPLATE_STATUSES,
  TemplateDraft,
  blankDraft,
  draftFrom,
  isDraftValid,
  moveStatus,
  toRequest,
  validateDraft
} from './workflow-template-draft';

/** Cài đặt · Mẫu workflow: mẫu có sẵn (chỉ đọc) và mẫu của tổ chức; mẫu mặc định được chọn sẵn khi tạo dự án. */
@Component({
  selector: 'app-org-workflow-templates',
  imports: [FormsModule, NgTemplateOutlet, SectionStateComponent, ModalShellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './org-workflow-templates.html'
})
export class OrgWorkflowTemplatesComponent implements OnInit {
  private readonly data = inject(WorkspaceDataSource);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly document = inject(DOCUMENT);
  private readonly context = inject(WorkspaceContextService);
  protected readonly ui = UI;
  protected readonly categories = Object.entries(STATUS_CATEGORY_LABELS) as [StatusCategory, string][];
  protected readonly maxStatuses = MAX_TEMPLATE_STATUSES;
  protected readonly managementTypes: ManagementType[] = ['Scrum', 'Kanban'];

  readonly loading = signal(true);
  readonly error = signal('');
  readonly templates = signal<WorkflowTemplate[]>([]);
  readonly busyId = signal<string | null>(null);
  readonly draft = signal<TemplateDraft | null>(null);
  readonly showErrors = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal('');

  readonly presets = computed(() => this.templates().filter(template => template.isPreset));
  readonly custom = computed(() => this.templates().filter(template => !template.isPreset));
  readonly draftErrors = computed(() => {
    const draft = this.draft();
    return draft ? validateDraft(draft) : { name: '', statuses: [], form: '' };
  });

  private get orgId(): string {
    return this.context.currentOrg()?.id ?? '';
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.data.listWorkflowTemplates(this.orgId).subscribe({
      next: templates => {
        this.templates.set(templates);
        this.loading.set(false);
      },
      error: error => {
        this.error.set(apiErrorMessage(error, 'Không tải được mẫu workflow.'));
        this.loading.set(false);
      }
    });
  }

  categoryLabel(category: StatusCategory): string {
    return STATUS_CATEGORY_LABELS[category];
  }

  chain(template: WorkflowTemplate): string {
    return template.statuses.map(status => status.name).join(' → ');
  }

  // --- Danh sách ------------------------------------------------------------

  setDefault(template: WorkflowTemplate): void {
    this.busyId.set(template.id);
    this.data.setDefaultWorkflowTemplate(this.orgId, template.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.templates.update(list => list.map(item => ({ ...item, isDefault: item.id === template.id })));
        this.toast.success('Đã đặt mẫu mặc định', template.name);
      },
      error: error => {
        this.busyId.set(null);
        this.toast.error('Không đặt được mẫu mặc định', apiErrorMessage(error, 'Thử lại sau.'));
      }
    });
  }

  remove(template: WorkflowTemplate): void {
    this.confirm.confirm({
      title: `Xóa mẫu “${template.name}”?`,
      message: 'Dự án đã tạo từ mẫu này vẫn giữ nguyên trạng thái của chúng.',
      type: 'danger',
      confirmText: 'Xóa mẫu',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.busyId.set(template.id);
        this.data.deleteWorkflowTemplate(this.orgId, template.id).subscribe({
          next: () => {
            this.busyId.set(null);
            this.templates.update(list => list.filter(item => item.id !== template.id));
            this.toast.success('Đã xóa mẫu', template.name);
          },
          error: error => {
            this.busyId.set(null);
            this.toast.error('Không xóa được mẫu', apiErrorMessage(error, 'Thử lại sau.'));
          }
        });
      }
    });
  }

  // --- Trình sửa ------------------------------------------------------------

  openNew(): void {
    this.openDraft(blankDraft());
  }

  openFrom(template: WorkflowTemplate): void {
    this.openDraft(draftFrom(template));
  }

  closeEditor(): void {
    this.draft.set(null);
  }

  updateDraft(change: Partial<TemplateDraft>): void {
    this.draft.update(draft => draft ? { ...draft, ...change } : draft);
    this.saveError.set('');
  }

  renameStatus(index: number, name: string): void {
    this.updateStatuses(statuses => statuses.map((status, i) => i === index ? { ...status, name } : status));
  }

  setCategory(index: number, category: StatusCategory): void {
    this.updateStatuses(statuses => statuses.map((status, i) => i === index ? { ...status, category } : status));
  }

  addStatus(): void {
    const count = this.draft()?.statuses.length ?? 0;
    if (count >= MAX_TEMPLATE_STATUSES) return;
    this.updateStatuses(statuses => [...statuses, { name: '', category: 'InProgress' }]);
    this.focusLater(`status-name-${count}`);
  }

  removeStatus(index: number): void {
    this.updateStatuses(statuses => statuses.filter((_, i) => i !== index));
  }

  /** Đổi thứ tự bằng nút ↑/↓ và giữ focus trên nút vừa bấm ở vị trí mới để dùng tiếp bằng bàn phím. */
  move(index: number, delta: -1 | 1): void {
    this.updateStatuses(statuses => moveStatus(statuses, index, delta));
    const target = index + delta;
    const length = this.draft()?.statuses.length ?? 0;
    if (target < 0 || target >= length) return;
    const edge = (delta === -1 && target === 0) || (delta === 1 && target === length - 1);
    this.focusLater(edge ? `status-${delta === -1 ? 'down' : 'up'}-${target}` : `status-${delta === -1 ? 'up' : 'down'}-${target}`);
  }

  save(): void {
    const draft = this.draft();
    if (!draft || this.saving()) return;
    this.showErrors.set(true);
    if (!isDraftValid(draft)) {
      const errors = validateDraft(draft);
      const statusIndex = errors.statuses.findIndex(error => error);
      this.focusLater(errors.name ? 'template-name' : statusIndex >= 0 ? `status-name-${statusIndex}` : 'template-name');
      return;
    }
    this.saving.set(true);
    this.saveError.set('');
    const request = toRequest(draft);
    const call: Observable<WorkflowTemplate> = draft.id
      ? this.data.updateWorkflowTemplate(this.orgId, draft.id, request)
      : this.data.createWorkflowTemplate(this.orgId, request);
    call.subscribe({
      next: saved => {
        this.saving.set(false);
        this.templates.update(list => draft.id ? list.map(item => item.id === saved.id ? saved : item) : [...list, saved]);
        this.draft.set(null);
        this.toast.success(draft.id ? 'Đã lưu mẫu' : 'Đã tạo mẫu', saved.name);
      },
      error: error => {
        this.saving.set(false);
        this.saveError.set(apiErrorMessage(error, 'Không lưu được mẫu workflow.'));
      }
    });
  }

  private openDraft(draft: TemplateDraft): void {
    this.showErrors.set(false);
    this.saveError.set('');
    this.draft.set(draft);
  }

  private updateStatuses(change: (statuses: TemplateDraft['statuses']) => TemplateDraft['statuses']): void {
    this.draft.update(draft => draft ? { ...draft, statuses: change(draft.statuses) } : draft);
    this.saveError.set('');
  }

  private focusLater(id: string): void {
    setTimeout(() => this.document.getElementById(id)?.focus());
  }
}
