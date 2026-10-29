import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { SectionStateComponent } from '../components/section-state';
import { UI } from '../components/ui-classes';
import { WorkspaceDataSource } from '../data/workspace-data-source';
import { WorkspaceProject } from '../data/workspace.models';
import { matchesSearch } from '../project-key';
import { WorkspaceContextService } from '../workspace-context.service';
import { WorkspaceNavigation } from '../workspace-navigation';

/** Dự án của tổ chức mà người dùng đã tham gia, có tìm kiếm theo tên hoặc mã. */
@Component({
  selector: 'app-org-projects',
  imports: [FormsModule, SectionStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './org-projects.html'
})
export class OrgProjectsComponent implements OnInit {
  private readonly data = inject(WorkspaceDataSource);
  protected readonly context = inject(WorkspaceContextService);
  protected readonly nav = inject(WorkspaceNavigation);
  protected readonly ui = UI;

  readonly projects = signal<WorkspaceProject[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly query = signal('');
  readonly filtered = computed(() => {
    const query = this.query();
    return this.projects().filter(project => matchesSearch(`${project.name} ${project.projectKey}`, query));
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const org = this.context.currentOrg();
    if (!org) return;
    this.loading.set(true);
    this.error.set('');
    this.data.listProjects(org.id).subscribe({
      next: projects => {
        this.projects.set(projects);
        this.loading.set(false);
      },
      error: error => {
        this.error.set(apiErrorMessage(error, 'Không tải được danh sách dự án.'));
        this.loading.set(false);
      }
    });
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('vi-VN');
  }
}
