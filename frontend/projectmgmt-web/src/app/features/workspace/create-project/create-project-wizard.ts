import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ModalShellComponent } from '../components/modal-shell';
import { UI } from '../components/ui-classes';
import { WorkspaceDataSource } from '../data/workspace-data-source';
import {
  CreateProjectRequest,
  DEFAULT_ISSUE_TYPES,
  IssueTypeCode,
  IssueTypeNameSetting,
  OrgMember,
  PROJECT_ROLE_OPTIONS,
  ProjectRoleCode,
  STATUS_CATEGORY_LABELS,
  WorkflowTemplate,
  WorkspaceProject
} from '../data/workspace.models';
import { isValidProjectKey, matchesSearch, normalizeProjectKey, suggestProjectKey } from '../project-key';
import { WorkspaceContextService } from '../workspace-context.service';

export type WizardStep = 1 | 2 | 3 | 'done';

export const ISSUE_TYPE_HINTS: Readonly<Record<IssueTypeCode, string>> = {
  Epic: 'Cấp cao nhất, gom nhiều Story/Task',
  Story: 'Yêu cầu theo góc nhìn người dùng',
  Task: 'Việc kỹ thuật cùng cấp Story',
  Bug: 'Lỗi cần sửa, cùng cấp Story',
  Subtask: 'Việc con, luôn thuộc một việc cha'
};

/** Wizard 3 bước tạo dự án trong tổ chức: thông tin → loại hình & kiểu công việc → thành viên. */
@Component({
  selector: 'app-create-project-wizard',
  imports: [FormsModule, ModalShellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './create-project-wizard.html'
})
export class CreateProjectWizardComponent implements OnInit {
  private readonly data = inject(WorkspaceDataSource);
  private readonly context = inject(WorkspaceContextService);

  readonly orgId = input.required<string>();
  readonly created = output<WorkspaceProject>();
  readonly openProject = output<WorkspaceProject>();
  readonly dismissed = output<void>();

  protected readonly ui = UI;
  protected readonly roleOptions = PROJECT_ROLE_OPTIONS;
  protected readonly categoryLabels = STATUS_CATEGORY_LABELS;
  protected readonly issueTypeHints = ISSUE_TYPE_HINTS;
  readonly steps = [
    { id: 1 as const, label: 'Thông tin' },
    { id: 2 as const, label: 'Loại hình & kiểu công việc' },
    { id: 3 as const, label: 'Thành viên' }
  ];

  readonly step = signal<WizardStep>(1);
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly busy = signal(false);
  readonly members = signal<OrgMember[]>([]);
  readonly templates = signal<WorkflowTemplate[]>([]);
  readonly createdProject = signal<WorkspaceProject | null>(null);
  readonly confirmingClose = signal(false);

  readonly name = signal('');
  readonly projectKey = signal('');
  readonly description = signal('');
  readonly leadUserId = signal('');
  readonly templateId = signal('');
  readonly issueTypes = signal<IssueTypeNameSetting[]>(DEFAULT_ISSUE_TYPES.map(type => ({ ...type })));
  readonly selected = signal<ReadonlyMap<string, ProjectRoleCode>>(new Map());
  readonly memberQuery = signal('');
  private keyEdited = false;

  readonly nameError = signal('');
  readonly keyError = signal('');
  readonly leadError = signal('');
  readonly step2Error = signal('');
  readonly stepError = signal('');

  readonly currentUserId = this.context.currentUserId;
  readonly selectedTemplate = computed(() => this.templates().find(template => template.id === this.templateId()) ?? null);
  readonly issueTypeErrors = computed(() => {
    const names = this.issueTypes().map(type => type.name.trim().toLowerCase());
    return this.issueTypes().map((type, index) => {
      const name = type.name.trim();
      if (name.length === 0) return 'Nhập tên.';
      if (name.length > 60) return 'Tối đa 60 ký tự.';
      return names.indexOf(name.toLowerCase()) !== index ? 'Trùng với kiểu khác.' : '';
    });
  });
  readonly issueTypesCustomized = computed(() => this.issueTypes().some((type, index) => type.name !== DEFAULT_ISSUE_TYPES[index].name));
  /** Người có thể chọn thêm: thành viên tổ chức trừ người tạo và người phụ trách (hai người này luôn trong dự án). */
  readonly candidates = computed(() => {
    const exclude = new Set([this.currentUserId(), this.leadUserId()]);
    const query = this.memberQuery();
    return this.members().filter(member => !exclude.has(member.userId) && matchesSearch(`${member.displayName} ${member.email}`, query));
  });
  readonly leadName = computed(() => this.members().find(member => member.userId === this.leadUserId())?.displayName ?? '');
  readonly isDirty = computed(() =>
    this.name().trim().length > 0 || this.description().trim().length > 0 || this.selected().size > 0 || this.issueTypesCustomized());

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set('');
    forkJoin({ members: this.data.listMembers(this.orgId()), templates: this.data.listWorkflowTemplates(this.orgId()) }).subscribe({
      next: ({ members, templates }) => {
        this.members.set(members);
        this.templates.set(templates);
        if (!this.leadUserId()) this.leadUserId.set(this.currentUserId() ?? members[0]?.userId ?? '');
        if (!templates.some(template => template.id === this.templateId())) {
          this.templateId.set((templates.find(template => template.isDefault) ?? templates[0])?.id ?? '');
        }
        this.loading.set(false);
      },
      error: error => {
        this.loadError.set(apiErrorMessage(error, 'Không tải được thành viên và mẫu workflow của tổ chức.'));
        this.loading.set(false);
      }
    });
  }

  // --- Bước 1 ---------------------------------------------------------------

  onNameChange(value: string): void {
    this.name.set(value);
    this.nameError.set('');
    if (!this.keyEdited) {
      this.projectKey.set(suggestProjectKey(value));
      this.keyError.set('');
    }
  }

  onKeyChange(value: string): void {
    const key = normalizeProjectKey(value);
    this.keyEdited = key.length > 0;
    this.projectKey.set(key);
    this.keyError.set('');
  }

  onLeadChange(userId: string): void {
    this.leadUserId.set(userId);
    this.leadError.set('');
    this.selected.update(current => {
      if (!current.has(userId)) return current;
      const next = new Map(current);
      next.delete(userId);
      return next;
    });
  }

  private validateStep1(): boolean {
    const name = this.name().trim();
    this.nameError.set(name.length < 2 ? 'Nhập tên dự án (ít nhất 2 ký tự).' : name.length > 100 ? 'Tên dự án tối đa 100 ký tự.' : '');
    this.keyError.set(isValidProjectKey(this.projectKey()) ? '' : 'Mã gồm 2–10 chữ in hoa hoặc số, bắt đầu bằng chữ.');
    this.leadError.set(this.members().some(member => member.userId === this.leadUserId()) ? '' : 'Chọn người phụ trách trong tổ chức.');
    if (this.description().trim().length > 1000) this.stepError.set('Mô tả tối đa 1000 ký tự.');
    return !this.nameError() && !this.keyError() && !this.leadError() && this.description().trim().length <= 1000;
  }

  // --- Bước 2 ---------------------------------------------------------------

  renameIssueType(code: IssueTypeCode, name: string): void {
    this.issueTypes.update(types => types.map(type => type.code === code ? { ...type, name } : type));
    this.step2Error.set('');
  }

  resetIssueTypes(): void {
    this.issueTypes.set(DEFAULT_ISSUE_TYPES.map(type => ({ ...type })));
  }

  private validateStep2(): boolean {
    if (!this.selectedTemplate()) {
      this.step2Error.set('Chọn một mẫu workflow cho dự án.');
      return false;
    }
    if (this.issueTypeErrors().some(error => error)) {
      this.step2Error.set('Kiểm tra lại tên các kiểu công việc.');
      return false;
    }
    this.step2Error.set('');
    return true;
  }

  // --- Bước 3 ---------------------------------------------------------------

  isSelected(userId: string): boolean {
    return this.selected().has(userId);
  }

  roleOf(userId: string): ProjectRoleCode {
    return this.selected().get(userId) ?? 'Developer';
  }

  toggleMember(userId: string, checked: boolean): void {
    this.selected.update(current => {
      const next = new Map(current);
      if (checked) next.set(userId, current.get(userId) ?? 'Developer');
      else next.delete(userId);
      return next;
    });
  }

  setMemberRole(userId: string, role: ProjectRoleCode): void {
    this.selected.update(current => new Map(current).set(userId, role));
  }

  // --- Điều hướng & gửi -----------------------------------------------------

  next(): void {
    this.stepError.set('');
    if (this.step() === 1) {
      if (!this.validateStep1()) return;
      this.busy.set(true);
      this.data.isProjectKeyAvailable(this.orgId(), this.projectKey()).subscribe({
        next: available => {
          this.busy.set(false);
          if (available) this.step.set(2);
          else this.keyError.set(`Mã ${this.projectKey()} đã được dùng trong tổ chức. Chọn mã khác.`);
        },
        error: error => {
          this.busy.set(false);
          this.stepError.set(apiErrorMessage(error, 'Không kiểm tra được mã dự án. Thử lại.'));
        }
      });
    } else if (this.step() === 2 && this.validateStep2()) {
      this.step.set(3);
    }
  }

  back(): void {
    this.stepError.set('');
    if (this.step() === 2) this.step.set(1);
    else if (this.step() === 3) this.step.set(2);
  }

  buildRequest(): CreateProjectRequest {
    return {
      name: this.name().trim(),
      projectKey: this.projectKey(),
      description: this.description().trim() || null,
      leadUserId: this.leadUserId(),
      workflowTemplateId: this.templateId(),
      issueTypes: this.issueTypes().map(type => ({ code: type.code, name: type.name.trim() })),
      members: [...this.selected()].map(([userId, role]) => ({ userId, role }))
    };
  }

  submit(): void {
    if (this.busy() || this.step() !== 3) return;
    this.busy.set(true);
    this.stepError.set('');
    this.data.createProject(this.orgId(), this.buildRequest()).subscribe({
      next: project => {
        this.busy.set(false);
        this.createdProject.set(project);
        this.step.set('done');
        this.created.emit(project);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.routeSubmitError(error);
      }
    });
  }

  /** Lỗi từ server được đưa về đúng bước và đúng trường để người dùng sửa. */
  private routeSubmitError(error: HttpErrorResponse): void {
    const message = apiErrorMessage(error, 'Không tạo được dự án. Thử lại sau.');
    const code: string = error.error?.code ?? '';
    if (code.startsWith('PROJECT_KEY')) {
      this.keyError.set(message);
      this.step.set(1);
    } else if (code === 'PROJECT_NAME_INVALID') {
      this.nameError.set(message);
      this.step.set(1);
    } else if (code === 'PROJECT_LEAD_INVALID') {
      this.leadError.set(message);
      this.step.set(1);
      this.load();
    } else if (code === 'PROJECT_DESCRIPTION_TOO_LONG') {
      this.stepError.set(message);
      this.step.set(1);
    } else if (code.startsWith('WORKFLOW_TEMPLATE') || code === 'ISSUE_TYPE_NAME_INVALID') {
      this.step2Error.set(message);
      this.step.set(2);
      if (code.startsWith('WORKFLOW_TEMPLATE')) this.load();
    } else {
      this.stepError.set(message);
    }
  }

  /** Đã nhập dữ liệu thì hỏi lại ngay trong wizard (hộp xác nhận nằm trong vùng giữ focus của modal). */
  requestClose(): void {
    if (this.step() === 'done' || !this.isDirty() || this.confirmingClose()) {
      this.dismissed.emit();
      return;
    }
    this.confirmingClose.set(true);
  }

  stepState(id: 1 | 2 | 3): 'done' | 'current' | 'todo' {
    const step = this.step();
    if (step === 'done' || (typeof step === 'number' && id < step)) return 'done';
    return step === id ? 'current' : 'todo';
  }

  statusChain(template: WorkflowTemplate): string {
    return template.statuses.map(status => status.name).join(' → ');
  }
}
