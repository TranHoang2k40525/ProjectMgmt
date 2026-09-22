import { Component, Input, Output, EventEmitter, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface ContextMenuItemAction {
  action: 'status' | 'assign' | 'ai-breakdown' | 'ai-assign' | 'priority' | 'copy' | 'delete';
  value?: any;
}

@Component({
  selector: 'app-context-menu',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div 
      *ngIf="visible" 
      class="fixed z-50 min-w-[220px] bg-slate-900/95 backdrop-blur-xl text-slate-100 border border-slate-700/80 rounded-xl shadow-2xl p-1.5 context-menu-panel"
      [style.left.px]="x"
      [style.top.px]="y"
      (click)="$event.stopPropagation()"
    >
      <div class="px-3 py-1.5 border-b border-slate-800 flex items-center justify-between text-xs text-slate-400 font-medium">
        <span class="font-mono text-slate-300 font-semibold">{{ itemKey || 'Tùy chọn nhanh' }}</span>
        <span class="material-symbols-outlined text-sm">more_vert</span>
      </div>

      <div class="py-1">
        <!-- Quick Status Section -->
        <div class="px-2 py-1 text-[11px] font-bold text-slate-400 tracking-wider uppercase">Chuyển trạng thái</div>
        <button (click)="select('status', 'TO_DO')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-slate-800 hover:text-white flex items-center justify-between transition-colors">
          <span class="flex items-center gap-2"><span class="w-2 h-2 rounded-full bg-amber-400"></span> To Do (Cần làm)</span>
        </button>
        <button (click)="select('status', 'IN_PROGRESS')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-slate-800 hover:text-white flex items-center justify-between transition-colors">
          <span class="flex items-center gap-2"><span class="w-2 h-2 rounded-full bg-blue-400"></span> In Progress (Đang làm)</span>
        </button>
        <button (click)="select('status', 'CODE_REVIEW')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-slate-800 hover:text-white flex items-center justify-between transition-colors">
          <span class="flex items-center gap-2"><span class="w-2 h-2 rounded-full bg-purple-400"></span> Code Review</span>
        </button>
        <button (click)="select('status', 'DONE')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-slate-800 hover:text-white flex items-center justify-between transition-colors">
          <span class="flex items-center gap-2"><span class="w-2 h-2 rounded-full bg-emerald-400"></span> Done (Hoàn thành)</span>
        </button>

        <div class="my-1 border-t border-slate-800"></div>

        <!-- AI Actions -->
        <button (click)="select('ai-breakdown')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg bg-gradient-to-r from-purple-900/40 to-indigo-900/40 text-purple-200 hover:from-purple-800/60 hover:to-indigo-800/60 flex items-center gap-2 font-medium transition-colors my-0.5">
          <span class="material-symbols-outlined text-sm text-purple-400">auto_awesome</span>
          <span>Phân rã Sub-task bằng AI 1</span>
        </button>
        
        <button (click)="select('ai-assign')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg bg-gradient-to-r from-blue-900/40 to-cyan-900/40 text-cyan-200 hover:from-blue-800/60 hover:to-cyan-800/60 flex items-center gap-2 font-medium transition-colors my-0.5">
          <span class="material-symbols-outlined text-sm text-cyan-400">psychology</span>
          <span>Đề xuất phân công bằng AI 2</span>
        </button>

        <div class="my-1 border-t border-slate-800"></div>

        <!-- General Actions -->
        <button (click)="select('assign', 'ME')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-slate-800 hover:text-white flex items-center gap-2 transition-colors">
          <span class="material-symbols-outlined text-sm text-slate-400">person</span>
          <span>Gán cho tôi</span>
        </button>

        <button (click)="select('copy')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-slate-800 hover:text-white flex items-center gap-2 transition-colors">
          <span class="material-symbols-outlined text-sm text-slate-400">content_copy</span>
          <span>Sao chép mã & liên kết</span>
        </button>

        <button (click)="select('delete')" class="w-full text-left px-3 py-1.5 text-xs rounded-lg hover:bg-rose-950 hover:text-rose-300 text-rose-400 flex items-center gap-2 transition-colors">
          <span class="material-symbols-outlined text-sm">delete</span>
          <span>Xóa thẻ</span>
        </button>
      </div>
    </div>
  `
})
export class ContextMenuComponent {
  @Input() visible = false;
  @Input() x = 0;
  @Input() y = 0;
  @Input() itemKey = '';
  @Output() action = new EventEmitter<ContextMenuItemAction>();
  @Output() close = new EventEmitter<void>();

  select(actionType: ContextMenuItemAction['action'], value?: any) {
    this.action.emit({ action: actionType, value });
    this.close.emit();
  }

  @HostListener('document:click')
  @HostListener('document:scroll')
  onDocumentClick() {
    if (this.visible) {
      this.close.emit();
    }
  }
}
