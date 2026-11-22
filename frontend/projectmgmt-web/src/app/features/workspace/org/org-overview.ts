import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { workspaceErrorMessage } from '../workspace-error';
import { RecentListComponent } from '../components/recent-list';
import { SectionStateComponent } from '../components/section-state';
import { UI } from '../components/ui-classes';
import { WorkListComponent } from '../components/work-list';
import { WorkspaceDataSource } from '../data/workspace-data-source';
import { RecentItem, WorkItemSummary, WorkspaceProject } from '../data/workspace.models';
import { WorkspaceContextService } from '../workspace-context.service';
import { WorkspaceNavigation } from '../workspace-navigation';

/** Tổng quan tổ chức: việc của mình trong tổ chức theo dự án, dự án mình phụ trách, lời mời chờ (chủ tổ chức), Gần đây. */
@Component({
  selector: 'app-org-overview',
  imports: [RouterLink, WorkListComponent, RecentListComponent, SectionStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid gap-6 lg:grid-cols-3">
      <section [class]="ui.card" class="min-w-0 lg:col-span-2" aria-labelledby="org-for-you">
        <h2 id="org-for-you" [class]="ui.sectionTitle" class="mb-3">Dành cho bạn</h2>

        @if (context.isOwner() && pendingInvites() > 0) {
          <a routerLink="../settings/members"
             class="mb-4 flex items-center gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900 hover:bg-amber-100 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-200">
            <span class="material-symbols-outlined" aria-hidden="true">mark_email_unread</span>
            <span class="flex-1"><span class="font-semibold tabular-nums">{{ pendingInvites() }}</span> lời mời chưa được phản hồi</span>
            <span class="font-semibold">Quản lý thành viên</span>
          </a>
        }

        <app-section-state [loading]="loading()" [error]="error()" [rows]="4" (retry)="load()">
          @if (ledProjects().length > 0) {
            <div class="mb-4">
              <h3 class="mb-2 text-xs font-bold uppercase text-slate-500">Dự án bạn phụ trách</h3>
              <ul class="flex flex-wrap gap-2">
                @for (project of ledProjects(); track project.id) {
                  <li>
                    <button type="button" (click)="nav.openProject(project)"
                            class="inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-slate-800 hover:border-primary-container dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100">
                      <span class="font-mono text-xs font-bold text-primary-container">{{ project.projectKey }}</span>
                      {{ project.name }}
                    </button>
                  </li>
                }
              </ul>
            </div>
          }
          <app-work-list [items]="work()" groupBy="project" (selected)="nav.openWorkItem($event)"
                         emptyText="Bạn không có việc nào đang mở trong tổ chức này." />
        </app-section-state>
      </section>

      <section [class]="ui.card" class="min-w-0" aria-labelledby="org-recent">
        <h2 id="org-recent" [class]="ui.sectionTitle" class="mb-3">Gần đây</h2>
        <app-section-state [loading]="loading()" [error]="error()" [rows]="5" (retry)="load()">
          <app-recent-list [items]="recent()" (selected)="nav.openRecent($event)"
                           emptyText="Dự án và công việc bạn mở trong tổ chức này sẽ hiện ở đây." />
        </app-section-state>
      </section>
    </div>
  `
})
export class OrgOverviewComponent implements OnInit {
  private readonly data = inject(WorkspaceDataSource);
  protected readonly context = inject(WorkspaceContextService);
  protected readonly nav = inject(WorkspaceNavigation);
  protected readonly ui = UI;

  readonly loading = signal(true);
  readonly error = signal('');
  readonly work = signal<WorkItemSummary[]>([]);
  readonly recent = signal<RecentItem[]>([]);
  readonly projects = signal<WorkspaceProject[]>([]);
  readonly pendingInvites = signal(0);
  readonly ledProjects = computed(() => this.projects().filter(project => project.leadUserId === this.context.currentUserId()));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const org = this.context.currentOrg();
    if (!org) return;
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      work: this.data.listMyWork(org.id),
      recent: this.data.listRecent(org.id),
      projects: this.data.listProjects(org.id)
    }).subscribe({
      next: ({ work, recent, projects }) => {
        this.work.set(work);
        this.recent.set(recent.filter(item => item.kind !== 'organization'));
        this.projects.set(projects);
        this.loading.set(false);
      },
      error: error => {
        this.error.set(workspaceErrorMessage(error, 'Không tải được tổng quan tổ chức.'));
        this.loading.set(false);
      }
    });
    if (this.context.isOwner()) {
      this.data.listInvitations(org.id).subscribe({ next: invites => this.pendingInvites.set(invites.length), error: () => this.pendingInvites.set(0) });
    }
  }
}
