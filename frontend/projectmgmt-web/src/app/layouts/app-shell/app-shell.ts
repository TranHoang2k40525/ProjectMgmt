import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SystemHealthService } from '../../core/services/system-health.service';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppShellComponent implements OnInit {
  protected readonly health = inject(SystemHealthService);

  protected readonly navigation = [
    { path: '/projects', label: 'Dự án' },
    { path: '/backlog', label: 'Backlog' },
    { path: '/board', label: 'Board' },
    { path: '/reports', label: 'Báo cáo' },
    { path: '/notifications', label: 'Thông báo' },
    { path: '/ai-dataops', label: 'AI DataOps' },
    { path: '/auth', label: 'Đăng nhập' }
  ];

  ngOnInit(): void {
    this.health.check();
  }
}
