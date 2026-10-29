import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { distinctUntilChanged, map, switchMap, tap } from 'rxjs';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ToastService } from '../../../core/services/toast.service';
import { ORG_ROLE_LABELS } from '../data/workspace.models';
import { WorkspaceContextService } from '../workspace-context.service';

/** Khung màn tổ chức: breadcrumb, thông tin tổ chức và các tab Tổng quan / Dự án / Cài đặt (chỉ chủ tổ chức). */
@Component({
  selector: 'app-org-page',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto w-full max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
      <nav aria-label="Đường dẫn" class="mb-3 text-sm text-slate-500">
        <ol class="flex flex-wrap items-center gap-1">
          <li><a routerLink="/for-you" class="hover:text-primary-container hover:underline">Trang chủ</a></li>
          <li aria-hidden="true" class="material-symbols-outlined text-[16px]">chevron_right</li>
          <li class="font-medium text-slate-700 dark:text-slate-200" aria-current="page">{{ org()?.name ?? 'Tổ chức' }}</li>
        </ol>
      </nav>

      @if (loading() && !org()) {
        <div class="h-24 rounded-2xl bg-slate-100 dark:bg-slate-800 animate-pulse" aria-busy="true"><span class="sr-only">Đang tải tổ chức…</span></div>
      } @else if (org(); as current) {
        <header class="flex flex-col sm:flex-row sm:items-center gap-4 mb-5">
          <span class="flex size-14 shrink-0 items-center justify-center rounded-2xl bg-primary-container text-2xl font-bold text-white" aria-hidden="true">
            {{ current.name.charAt(0).toUpperCase() }}
          </span>
          <div class="min-w-0 flex-1">
            <h1 class="text-2xl font-bold text-slate-900 dark:text-white text-balance">{{ current.name }}</h1>
            <p class="text-sm text-slate-500">
              {{ roleLabels[current.myRole] }} ·
              <span class="tabular-nums">{{ current.projectCount }}</span> dự án ·
              <span class="tabular-nums">{{ current.memberCount }}</span> thành viên
            </p>
          </div>
        </header>

        <nav aria-label="Mục của tổ chức" class="mb-6 border-b border-slate-200 dark:border-slate-800 overflow-x-auto">
          <ul class="flex gap-1 min-w-max">
            @for (tab of tabs(); track tab.path) {
              <li>
                <a [routerLink]="tab.path" routerLinkActive="!border-primary-container !text-primary-container" ariaCurrentWhenActive="page"
                   class="inline-flex items-center gap-1.5 border-b-2 border-transparent px-3 py-2.5 text-sm font-semibold text-slate-600 hover:text-slate-900 dark:text-slate-300 dark:hover:text-white">
                  <span class="material-symbols-outlined text-[18px]" aria-hidden="true">{{ tab.icon }}</span>
                  {{ tab.label }}
                </a>
              </li>
            }
          </ul>
        </nav>

        <router-outlet />
      }
    </div>
  `
})
export class OrgPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly context = inject(WorkspaceContextService);

  protected readonly roleLabels = ORG_ROLE_LABELS;
  readonly loading = signal(true);
  readonly org = this.context.currentOrg;
  readonly tabs = computed(() => {
    const base = [
      { path: 'overview', label: 'Tổng quan', icon: 'dashboard' },
      { path: 'projects', label: 'Dự án', icon: 'folder' }
    ];
    return this.context.isOwner() ? [...base, { path: 'settings', label: 'Cài đặt', icon: 'settings' }] : base;
  });

  ngOnInit(): void {
    this.route.paramMap.pipe(
      map(params => params.get('orgId') ?? ''),
      distinctUntilChanged(),
      tap(orgId => {
        this.loading.set(true);
        if (this.context.currentOrg()?.id !== orgId) this.context.currentOrg.set(null);
      }),
      switchMap(orgId => this.context.openOrganization(orgId)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => this.loading.set(false),
      error: error => {
        this.loading.set(false);
        this.toast.error('Không mở được tổ chức', apiErrorMessage(error, 'Tổ chức không tồn tại hoặc bạn không còn là thành viên.'));
        void this.router.navigate(['/for-you']);
      }
    });
  }
}
