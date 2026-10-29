import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** Khung Cài đặt tổ chức (chỉ chủ tổ chức vào được, xem orgOwnerGuard). */
@Component({
  selector: 'app-org-settings',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid gap-6 md:grid-cols-[13rem_1fr]">
      <nav aria-label="Cài đặt tổ chức">
        <ul class="flex gap-1 overflow-x-auto md:flex-col">
          @for (item of items; track item.path) {
            <li>
              <a [routerLink]="item.path" routerLinkActive="!bg-primary-fixed !text-primary" ariaCurrentWhenActive="page"
                 class="flex items-center gap-2 whitespace-nowrap rounded-lg px-3 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800">
                <span class="material-symbols-outlined text-[18px]" aria-hidden="true">{{ item.icon }}</span>
                {{ item.label }}
              </a>
            </li>
          }
        </ul>
      </nav>
      <div class="min-w-0">
        <router-outlet />
      </div>
    </div>
  `
})
export class OrgSettingsComponent {
  readonly items = [
    { path: 'members', label: 'Thành viên', icon: 'group' },
    { path: 'workflows', label: 'Quy trình workflow', icon: 'account_tree' }
  ];
}
