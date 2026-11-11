import { Component, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

export type AnimatedIconType =
  | 'settings'
  | 'gear'
  | 'sync'
  | 'workflow'
  | 'check'
  | 'done'
  | 'task'
  | 'story'
  | 'bug'
  | 'epic'
  | 'bolt'
  | 'loading'
  | 'spinner'
  | 'bell'
  | 'delete'
  | 'trash'
  | 'group'
  | 'members'
  | 'folder'
  | 'add'
  | 'plus'
  | 'close'
  | 'arrow'
  | 'shield'
  | 'palette'
  | 'chart'
  | 'analytics'
  | 'search';

@Component({
  selector: 'app-animated-icon',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span
      class="inline-flex items-center justify-center shrink-0 transition-transform duration-300"
      [ngClass]="wrapperClasses()"
      [style.width.px]="size"
      [style.height.px]="size"
      [style.color]="color || 'currentColor'"
    >
      @switch (normalizedName()) {
        @case ('settings') {
          <!-- Animated Rotating Gear -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-gear">
            <path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/>
            <circle cx="12" cy="12" r="3"/>
          </svg>
        }

        @case ('sync') {
          <!-- Animated 2-Way Sync Arrows -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-sync">
            <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67"/>
          </svg>
        }

        @case ('loading') {
          <!-- High-Tech Rotating Segment Spinner -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" class="icon-spin">
            <circle cx="12" cy="12" r="10" stroke-opacity="0.2" />
            <path d="M12 2a10 10 0 0 1 10 10" stroke-linecap="round" />
          </svg>
        }

        @case ('check') {
          <!-- Animated Checkmark -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" class="animated-check">
            <circle cx="12" cy="12" r="10" class="circle-path"/>
            <path d="m9 12 2 2 4-4" class="check-path"/>
          </svg>
        }

        @case ('bolt') {
          <!-- Glowing Electric Lightning (Epic) -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="currentColor" class="animated-bolt">
            <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/>
          </svg>
        }

        @case ('bell') {
          <!-- Ringing Notification Bell -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-bell">
            <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/>
            <path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/>
          </svg>
        }

        @case ('delete') {
          <!-- Animated Trash Can -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-trash">
            <path d="M3 6h18"/>
            <path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/>
            <path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/>
            <line x1="10" y1="11" x2="10" y2="17"/>
            <line x1="14" y1="11" x2="14" y2="17"/>
          </svg>
        }

        @case ('group') {
          <!-- Team / Members Silhouette -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-group">
            <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/>
            <circle cx="9" cy="7" r="4"/>
            <path d="M22 21v-2a4 4 0 0 0-3-3.87"/>
            <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
          </svg>
        }

        @case ('add') {
          <!-- Rotating Plus / Create -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" class="animated-add">
            <line x1="12" y1="5" x2="12" y2="19"/>
            <line x1="5" y1="12" x2="19" y2="12"/>
          </svg>
        }

        @case ('close') {
          <!-- Close Cross -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" class="animated-close">
            <line x1="18" y1="6" x2="6" y2="18"/>
            <line x1="6" y1="6" x2="18" y2="18"/>
          </svg>
        }

        @case ('arrow') {
          <!-- Bouncing Forward Arrow -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-arrow">
            <line x1="5" y1="12" x2="19" y2="12"/>
            <polyline points="12 5 19 12 12 19"/>
          </svg>
        }

        @case ('folder') {
          <!-- Open Folder -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-folder">
            <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z"/>
            <path d="M2 10h20"/>
          </svg>
        }

        @case ('shield') {
          <!-- Security Shield -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-shield">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
          </svg>
        }

        @case ('palette') {
          <!-- Design Palette -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-palette">
            <circle cx="13.5" cy="6.5" r=".5" fill="currentColor"/>
            <circle cx="17.5" cy="10.5" r=".5" fill="currentColor"/>
            <circle cx="8.5" cy="7.5" r=".5" fill="currentColor"/>
            <circle cx="6.5" cy="12.5" r=".5" fill="currentColor"/>
            <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z"/>
          </svg>
        }

        @case ('analytics') {
          <!-- Analytics Chart -->
          <svg [attr.width]="size" [attr.height]="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animated-chart">
            <path d="M3 3v18h18"/>
            <path d="m19 9-5 5-4-4-3 3"/>
          </svg>
        }

        @default {
          <!-- Fallback: Material Symbol with smooth animated CSS micro-transitions -->
          <span class="material-symbols-outlined shrink-0" [style.fontSize.px]="size">
            {{ name }}
          </span>
        }
      }
    </span>
  `,
  styles: [`
    :host {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      vertical-align: middle;
    }

    /* Gear Rotate on Hover / Trigger */
    .animated-gear {
      transform-origin: center;
      transition: transform 0.5s cubic-bezier(0.16, 1, 0.3, 1);
    }
    :host(:hover) .animated-gear,
    :host-context(.group:hover) .animated-gear {
      transform: rotate(90deg);
    }

    /* Sync Rotate on Hover */
    .animated-sync {
      transform-origin: center;
      transition: transform 0.6s cubic-bezier(0.16, 1, 0.3, 1);
    }
    :host(:hover) .animated-sync,
    :host-context(.group:hover) .animated-sync {
      transform: rotate(180deg);
    }

    /* Bolt pulse */
    .animated-bolt {
      transition: transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
    }
    :host(:hover) .animated-bolt,
    :host-context(.group:hover) .animated-bolt {
      transform: scale(1.2);
    }

    /* Check animation */
    .circle-path {
      stroke-dasharray: 63;
      stroke-dashoffset: 0;
      transition: stroke-dashoffset 0.4s ease;
    }
    .check-path {
      stroke-dasharray: 10;
      stroke-dashoffset: 0;
      transition: stroke-dashoffset 0.3s ease 0.1s;
    }

    /* Bell Ringing */
    .animated-bell {
      transform-origin: top center;
      transition: transform 0.3s ease;
    }
    :host(:hover) .animated-bell,
    :host-context(.group:hover) .animated-bell {
      animation: icon-bell-ring 1s ease-in-out;
    }

    /* Trash Shake */
    .animated-trash {
      transform-origin: bottom center;
      transition: transform 0.2s ease;
    }
    :host(:hover) .animated-trash,
    :host-context(.group:hover) .animated-trash {
      animation: icon-wiggle-subtle 0.5s ease-in-out;
    }

    /* Arrow Forward */
    .animated-arrow {
      transition: transform 0.2s ease-out;
    }
    :host(:hover) .animated-arrow,
    :host-context(.group:hover) .animated-arrow {
      transform: translateX(3px);
    }

    /* Add Plus Rotate */
    .animated-add {
      transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
    }
    :host(:hover) .animated-add,
    :host-context(.group:hover) .animated-add {
      transform: rotate(90deg);
    }

    /* Close Scale */
    .animated-close {
      transition: transform 0.2s ease-in-out;
    }
    :host(:hover) .animated-close {
      transform: scale(1.15);
    }

    /* Group pulse */
    .animated-group {
      transition: transform 0.25s ease-out;
    }
    :host(:hover) .animated-group,
    :host-context(.group:hover) .animated-group {
      transform: scale(1.08);
    }

    /* Folder open */
    .animated-folder {
      transition: transform 0.25s ease-out;
    }
    :host(:hover) .animated-folder,
    :host-context(.group:hover) .animated-folder {
      transform: translateY(-1px);
    }
  `]
})
export class AnimatedIconComponent {
  @Input() name: AnimatedIconType | string = 'settings';
  @Input() size = 20;
  @Input() color?: string;
  @Input() customClass = '';
  @Input() animation?: 'spin' | 'pulse' | 'wiggle' | 'bell';

  readonly normalizedName = computed(() => {
    const raw = (this.name || '').toLowerCase().trim();
    if (raw === 'gear') return 'settings';
    if (raw === 'workflow') return 'sync';
    if (raw === 'done') return 'check';
    if (raw === 'spinner') return 'loading';
    if (raw === 'trash') return 'delete';
    if (raw === 'members') return 'group';
    if (raw === 'plus') return 'add';
    if (raw === 'epic') return 'bolt';
    if (raw === 'chart') return 'analytics';
    return raw;
  });

  readonly wrapperClasses = computed(() => {
    const classes = [this.customClass];
    if (this.animation === 'spin') classes.push('icon-spin');
    if (this.animation === 'pulse') classes.push('icon-pulse');
    if (this.animation === 'wiggle') classes.push('icon-wiggle');
    if (this.animation === 'bell') classes.push('icon-bell');
    return classes.join(' ');
  });
}
