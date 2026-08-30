import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-avatar',
  template: `
    <span class="avatar" [style.width.px]="size()" [style.height.px]="size()">
      @if (url()) { <img [src]="url()" [alt]="displayName()" /> } @else { {{ initials() }} }
    </span>
  `,
  styles: `.avatar{align-items:center;background:#dff6f0;border-radius:50%;color:#087d67;display:inline-flex;font-size:.72rem;font-weight:750;justify-content:center;overflow:hidden}.avatar img{height:100%;object-fit:cover;width:100%}`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AvatarComponent {
  readonly displayName = input.required<string>();
  readonly url = input<string | null>(null);
  readonly size = input(32);
  readonly initials = computed(() => this.displayName().split(/\s+/).filter(Boolean).slice(-2).map(part => part[0]).join('').toUpperCase());
}
