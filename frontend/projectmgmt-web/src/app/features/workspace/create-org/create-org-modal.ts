import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ModalShellComponent } from '../components/modal-shell';
import { UI } from '../components/ui-classes';
import { WorkspaceDataSource } from '../data/workspace-data-source';
import { OrganizationSummary } from '../data/workspace.models';
import { suggestSlug } from '../project-key';

@Component({
  selector: 'app-create-org-modal',
  imports: [FormsModule, ModalShellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-modal-shell heading="Tạo tổ chức" description="Tổ chức gom các dự án và thành viên của một nhóm, phòng ban hoặc công ty."
                     (closeRequested)="dismissed.emit()">
      <form id="create-org-form" (ngSubmit)="submit()" novalidate class="space-y-4">
        <div>
          <label for="org-name" [class]="ui.label">Tên tổ chức</label>
          <input id="org-name" name="name" [class]="ui.input" [ngModel]="name()" (ngModelChange)="onNameChange($event)"
                 maxlength="100" autocomplete="organization" required
                 [attr.aria-invalid]="!!nameError()" [attr.aria-describedby]="nameError() ? 'org-name-error' : null" />
          @if (nameError()) {
            <p id="org-name-error" [class]="ui.fieldError">{{ nameError() }}</p>
          }
        </div>
        <div>
          <label for="org-slug" [class]="ui.label">Đường dẫn</label>
          <input id="org-slug" name="slug" [class]="ui.input" [ngModel]="slug()" (ngModelChange)="onSlugChange($event)"
                 maxlength="60" spellcheck="false" autocapitalize="off"
                 [attr.aria-invalid]="!!slugError()" [attr.aria-describedby]="slugError() ? 'org-slug-error' : 'org-slug-hint'" />
          @if (slugError()) {
            <p id="org-slug-error" [class]="ui.fieldError">{{ slugError() }}</p>
          } @else {
            <p id="org-slug-hint" [class]="ui.hint">Tự tạo theo tên; chỉ gồm chữ thường, số và dấu gạch nối.</p>
          }
        </div>
        @if (formError()) {
          <p role="alert" class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800 dark:bg-red-950/40 dark:text-red-300">{{ formError() }}</p>
        }
      </form>
      <ng-container modal-footer>
        <button type="button" [class]="ui.secondaryButton" (click)="dismissed.emit()">Hủy</button>
        <button type="submit" form="create-org-form" [class]="ui.primaryButton" [disabled]="saving()">
          {{ saving() ? 'Đang tạo…' : 'Tạo tổ chức' }}
        </button>
      </ng-container>
    </app-modal-shell>
  `
})
export class CreateOrgModalComponent {
  private readonly data = inject(WorkspaceDataSource);

  readonly created = output<OrganizationSummary>();
  readonly dismissed = output<void>();

  protected readonly ui = UI;
  readonly name = signal('');
  readonly slug = signal('');
  readonly nameError = signal('');
  readonly slugError = signal('');
  readonly formError = signal('');
  readonly saving = signal(false);
  private slugEdited = false;

  onNameChange(value: string): void {
    this.name.set(value);
    this.nameError.set('');
    if (!this.slugEdited) this.slug.set(suggestSlug(value));
  }

  onSlugChange(value: string): void {
    this.slugEdited = value.trim().length > 0;
    this.slug.set(value.toLowerCase());
    this.slugError.set('');
  }

  submit(): void {
    if (this.saving()) return;
    const name = this.name().trim();
    const slug = this.slug().trim();
    this.nameError.set(name.length < 2 ? 'Nhập tên tổ chức (ít nhất 2 ký tự).' : '');
    this.slugError.set(/^[a-z0-9]+(-[a-z0-9]+)*$/.test(slug) && slug.length >= 2 ? '' : 'Đường dẫn chỉ gồm chữ thường, số và dấu gạch nối (2–60 ký tự).');
    if (this.nameError() || this.slugError()) return;

    this.saving.set(true);
    this.formError.set('');
    this.data.createOrganization({ name, slug }).subscribe({
      next: org => {
        this.saving.set(false);
        this.created.emit(org);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        const message = apiErrorMessage(error, 'Không tạo được tổ chức. Thử lại sau.');
        if (error.error?.code?.startsWith('ORG_SLUG')) this.slugError.set(message);
        else if (error.error?.code === 'ORG_NAME_INVALID') this.nameError.set(message);
        else this.formError.set(message);
      }
    });
  }
}
