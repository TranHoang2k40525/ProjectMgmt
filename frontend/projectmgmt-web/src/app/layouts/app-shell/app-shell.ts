import { ChangeDetectionStrategy, Component, OnInit, ElementRef, inject, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet, Router } from '@angular/router';
import { IdentityService } from '../../core/services/identity.service';
import { gsap } from 'gsap';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppShellComponent implements OnInit, AfterViewInit {
  protected readonly identity = inject(IdentityService);
  private readonly router = inject(Router);
  private readonly el = inject(ElementRef);

  protected searchQuery = '';

  protected readonly mainNav = [
    { path: '/projects', label: 'Tổng quan (Projects)', icon: 'space_dashboard' },
    { path: '/board', label: 'Bảng Scrum (Board)', icon: 'view_kanban' },
    { path: '/backlog', label: 'Kế hoạch Backlog', icon: 'splitscreen' },
    { path: '/notifications', label: 'Thông báo Realtime', icon: 'notifications', badge: true },
  ];

  protected readonly settingsNav = [
    { path: '/profile', label: 'Hồ sơ & Bảo mật', icon: 'person' },
    { path: '/admin/identity', label: 'Quản trị RBAC', icon: 'verified_user' },
    { path: '/admin/ai-governance', label: 'Quản trị AI', icon: 'psychology' }
  ];

  ngOnInit(): void {
    // Check if user is authenticated; if not, route to auth (unauthenticated mode)
  }

  ngAfterViewInit(): void {
    const ctx = gsap.context(() => {
      // Smooth entrance stagger for sidebar links using GSAP
      gsap.from('.jira-nav-link', {
        opacity: 0,
        x: -16,
        duration: 0.4,
        stagger: 0.05,
        ease: 'power2.out'
      });
    }, this.el.nativeElement);
  }

  logout(): void {
    this.identity.logout();
    this.router.navigateByUrl('/auth');
  }
}
