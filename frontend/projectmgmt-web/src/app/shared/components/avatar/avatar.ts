import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { resolveAvatarUrl } from '../../../core/utils/avatar.utils';

@Component({
  selector: 'app-avatar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="avatar shrink-0 select-none shadow-2xs" [style.width.px]="size()" [style.height.px]="size()">
      @if (resolvedUrl() && !hasError()) {
        <img [src]="resolvedUrl()" [alt]="displayName()" (error)="hasError.set(true)" />
      } @else {
        <span class="avatar-initials">{{ initials() }}</span>
      }
    </span>
  `,
  styles: `
    .avatar {
      align-items: center;
      background: linear-gradient(135deg, #0284c7 0%, #0369a1 100%);
      border-radius: 50%;
      color: #ffffff;
      display: inline-flex;
      font-size: 0.72rem;
      font-weight: 700;
      justify-content: center;
      overflow: hidden;
      border: 1px solid rgba(255, 255, 255, 0.2);
    }
    .avatar img {
      height: 100%;
      object-fit: cover;
      width: 100%;
    }
    .avatar-initials {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      line-height: 1;
      letter-spacing: -0.02em;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AvatarComponent {
  readonly displayName = input.required<string>();
  readonly url = input<string | null | undefined>(null);
  readonly size = input(32);

  readonly hasError = signal(false);

  readonly resolvedUrl = computed(() => {
    this.hasError.set(false); // Reset error state on URL change
    return resolveAvatarUrl(this.url());
  });

  readonly initials = computed(() => {
    const name = this.displayName() || '';
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  });
}
