import { Component, Input, Output, EventEmitter, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import type { ProjectStatus } from '../../core/services/project-management.service';

export interface ContextMenuItemAction {
  action: 'status' | 'assign' | 'ai-breakdown' | 'ai-assign' | 'priority' | 'copy' | 'delete';
  value?: string;
}

@Component({
  selector: 'app-context-menu',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible) {
      <div 
        tabindex="0"
        role="menu"
        aria-label="Menu tùy chọn"
        class="fixed z-50 min-w-[235px] max-w-[290px] max-h-[85vh] overflow-y-auto bg-slate-900/95 backdrop-blur-xl text-slate-100 border border-slate-700/80 rounded-xl shadow-2xl p-1.5 context-menu-panel focus:outline-none"
        [style.left.px]="x"
        [style.top.px]="y"
        (click)="$event.stopPropagation()"
        (keydown)="$event.stopPropagation()"
      >
        <div class="px-3 py-1.5 border-b border-slate-800 flex items-center justify-between text-xs text-slate-400 font-medium">
          <span class="font-mono text-slate-300 font-semibold">{{ itemKey || 'Tùy chọn nhanh' }}</span>
          <span class="material-symbols-outlined text-sm">more_vert</span>
        </div>

        <div class="py-1">
          @if (canTransition) {
            <!-- Quick Status Section (Dynamic Active Workflow) -->
          <div class="px-2 py-1 flex items-center justify-between gap-2">
            <span class="text-[11px] font-bold text-slate-400 tracking-wider uppercase">Chuyển trạng thái</span>
            @if (workflowName) {
              <span class="text-[10px] font-medium text-slate-400 bg-slate-800/90 px-1.5 py-0.5 rounded truncate max-w-[110px]" [title]="workflowName">
                {{ workflowName }}
              </span>
            }
          </div>
          <div class="max-h-[220px] overflow-y-auto space-y-0.5">
            @for (st of orderedStatuses; track st.id || st.name) {
              <button
                type="button"
                (click)="select('status', st.name)"
                [class.bg-slate-800]="isCurrentStatus(st.name)"
                [class.text-white]="isCurrentStatus(st.name)"
                class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-slate-800 hover:text-white flex items-center justify-between gap-2 transition-colors"
              >
                <span class="flex items-center gap-2 min-w-0">
                  <span class="w-2 h-2 rounded-full shrink-0" [style.backgroundColor]="resolveStatusColor(st)"></span>
                  <span class="truncate">{{ st.name }}</span>
                </span>
                @if (isCurrentStatus(st.name)) {
                  <span class="material-symbols-outlined text-[15px] text-emerald-400 shrink-0" aria-hidden="true">check</span>
                }
              </button>
            } @empty {
              <div class="px-3 py-1.5 text-xs text-slate-400">Chưa có cấu hình trạng thái</div>
            }
          </div>

          <div class="my-1 border-t border-slate-800"></div>
          }

          @if (canAiBreakdown || canAiAssign) {
            <!-- AI Actions -->
            @if (canAiBreakdown) {
              <button (click)="select('ai-breakdown')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg bg-gradient-to-r from-purple-900/40 to-indigo-900/40 text-purple-200 hover:from-purple-800/60 hover:to-indigo-800/60 flex items-center gap-2 font-medium transition-colors my-0.5">
                <span class="material-symbols-outlined text-sm text-purple-400">auto_awesome</span>
                <span>Phân rã Sub-task bằng AI</span>
              </button>
            }

            @if (canAiAssign) {
              <button (click)="select('ai-assign')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg bg-gradient-to-r from-blue-900/40 to-cyan-900/40 text-cyan-200 hover:from-blue-800/60 hover:to-cyan-800/60 flex items-center gap-2 font-medium transition-colors my-0.5">
                <span class="material-symbols-outlined text-sm text-cyan-400">psychology</span>
                <span>Đề xuất phân công bằng AI</span>
              </button>
            }

            <div class="my-1 border-t border-slate-800"></div>
          }

          <!-- General Actions -->
          @if (canAssign) {
            <button (click)="select('assign', 'ME')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-slate-800 hover:text-white flex items-center gap-2 transition-colors">
              <span class="material-symbols-outlined text-sm text-slate-400">person</span>
              <span>Gán cho tôi</span>
            </button>
          }

          <button (click)="select('copy')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-slate-800 hover:text-white flex items-center gap-2 transition-colors">
            <span class="material-symbols-outlined text-sm text-slate-400">content_copy</span>
            <span>Sao chép mã & liên kết</span>
          </button>

          @if (canDelete) {
            <button (click)="select('delete')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-rose-950 hover:text-rose-300 text-rose-400 flex items-center gap-2 transition-colors">
              <span class="material-symbols-outlined text-sm">delete</span>
              <span>Xóa thẻ</span>
            </button>
          }
        </div>
      </div>
    }
  `
})
export class ContextMenuComponent {
  @Input() visible = false;
  @Input() x = 0;
  @Input() y = 0;
  @Input() itemKey = '';
  @Input() statuses: ProjectStatus[] = [];
  @Input() currentStatus = '';
  @Input() workflowName = '';
  @Input() canTransition = true;
  @Input() canAssign = true;
  @Input() canDelete = true;
  @Input() canAiBreakdown = true;
  @Input() canAiAssign = true;
  @Output() action = new EventEmitter<ContextMenuItemAction>();
  @Output() menuClose = new EventEmitter<void>();

  get orderedStatuses(): ProjectStatus[] {
    return [...(this.statuses ?? [])].sort((a, b) => (a.orderIndex ?? 0) - (b.orderIndex ?? 0));
  }

  isCurrentStatus(statusName: string): boolean {
    return statusName.trim().toLowerCase() === (this.currentStatus || '').trim().toLowerCase();
  }

  resolveStatusColor(st: ProjectStatus): string {
    if (st.colorHex?.trim()) return st.colorHex;
    const category = (st.category || st.name || '').trim().toLowerCase();
    if (st.isDone || category === 'done') return '#10B981';
    if (st.isCanceled || category === 'canceled' || category === 'cancelled') return '#EF4444';
    if (st.isActiveWork || category === 'inprogress' || category === 'in progress') return '#3B82F6';
    if (st.isInitial || category === 'todo' || category === 'to do') return '#F59E0B';
    return '#64748B';
  }

  select(actionType: ContextMenuItemAction['action'], value?: string) {
    this.action.emit({ action: actionType, value });
    this.menuClose.emit();
  }

  @HostListener('document:click')
  @HostListener('document:scroll')
  onDocumentClick() {
    if (this.visible) {
      this.menuClose.emit();
    }
  }
}
