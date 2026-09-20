import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  Board,
  BoardColumn,
  CreateBoardColumnRequest,
  CreateBoardRequest,
  Project,
  ProjectManagementService,
  ProjectStatus
} from '../../core/services/project-management.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-board-settings-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="board-settings-container">
      <div class="breadcrumb">
        <a routerLink="/projects">Dự án</a> &gt;
        @if (project()) {
          <a [routerLink]="['/projects', projectId, 'settings']">{{ project()?.name }}</a> &gt;
        }
        <span>Cấu hình Board</span>
      </div>

      <div class="page-header">
        <div>
          <h1>Cấu hình Board & Cột công việc (WIP Limit)</h1>
          <p class="subtitle">Quản lý các cột hiển thị trên Kanban/Scrum Board và giới hạn công việc đang xử lý (WIP)</p>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="settings-tabs">
        <a [routerLink]="['/projects', projectId, 'settings']" class="tab-item">⚙️ Thông tin chung</a>
        <a [routerLink]="['/projects', projectId, 'settings', 'workflow']" class="tab-item">🔄 Cấu hình Workflow</a>
        <span class="tab-item active">📋 Cấu hình Board</span>
      </div>

      @if (alertMessage()) {
        <div class="alert" [class.alert-success]="alertType() === 'success'" [class.alert-error]="alertType() === 'error'">
          {{ alertMessage() }}
        </div>
      }

      <!-- Board Selector / Creator -->
      <div class="card board-select-box">
        <div class="board-header">
          <div>
            <h3>Chọn Bảng (Board) để cấu hình</h3>
            <div class="board-tabs">
              @for (b of boards(); track b.id) {
                <button
                  type="button"
                  (click)="selectBoard(b)"
                  class="board-btn"
                  [class.active]="selectedBoard()?.id === b.id"
                >
                  <span class="board-type-tag">{{ b.type }}</span>
                  <strong>{{ b.name }}</strong>
                  @if (b.isDefault) {
                    <span class="default-pill">Mặc định</span>
                  }
                </button>
              }
            </div>
          </div>
          <button (click)="showNewBoardModal.set(!showNewBoardModal())" class="btn btn-secondary btn-sm">
            + Tạo Board mới
          </button>
        </div>

        @if (showNewBoardModal()) {
          <div class="new-board-form">
            <h4>Tạo Board mới</h4>
            <div class="form-row">
              <input
                type="text"
                [(ngModel)]="newBoard.name"
                placeholder="Tên board (VD: Sprint Board, Kanban Fast-track)"
                class="form-control"
              />
              <select [(ngModel)]="newBoard.type" class="form-control" style="max-width: 160px;">
                <option value="Kanban">Kanban</option>
                <option value="Scrum">Scrum</option>
              </select>
              <button (click)="onCreateBoard()" class="btn btn-primary btn-sm">Tạo</button>
              <button (click)="showNewBoardModal.set(false)" class="btn btn-secondary btn-sm">Hủy</button>
            </div>
          </div>
        }
      </div>

      @if (selectedBoard()) {
        <div class="columns-management-grid">
          <!-- Columns List -->
          <div class="card">
            <div class="columns-header">
              <div>
                <h3>Danh sách Cột trên Board: {{ selectedBoard()?.name }}</h3>
                <p class="hint">Dùng các nút mũi tên ▲ ▼ để sắp xếp lại thứ tự cột từ trái sang phải trên bảng.</p>
              </div>
            </div>

            @if (columnsLoading()) {
              <div class="loading-state">
                <div class="spinner"></div>
                <p>Đang tải danh sách cột...</p>
              </div>
            } @else if (columns().length === 0) {
              <div class="empty-columns">
                <p>Board này chưa có cột nào. Hãy thêm cột đầu tiên bên dưới.</p>
              </div>
            } @else {
              <div class="column-list">
                @for (col of columns(); track col.id; let i = $index) {
                  <div class="column-item">
                    <div class="col-drag-handle">
                      <button
                        (click)="moveColumn(i, -1)"
                        [disabled]="i === 0"
                        class="btn-arrow"
                        title="Di chuyển sang trái"
                      >▲</button>
                      <span class="order-idx">{{ col.orderIndex }}</span>
                      <button
                        (click)="moveColumn(i, 1)"
                        [disabled]="i === columns().length - 1"
                        class="btn-arrow"
                        title="Di chuyển sang phải"
                      >▼</button>
                    </div>

                    <div class="col-info">
                      <div class="col-title">
                        <strong>{{ col.name || col.statusName }}</strong>
                        <span class="status-linked">Map: {{ col.statusName }}</span>
                      </div>
                      <div class="col-wip">
                        @if (col.wipLimit !== null && col.wipLimit !== undefined) {
                          <span class="wip-badge">WIP Limit: <strong>{{ col.wipLimit }}</strong></span>
                        } @else {
                          <span class="wip-badge wip-unlimited">Không giới hạn WIP</span>
                        }
                      </div>
                    </div>

                    <div class="col-actions">
                      <button (click)="onDeleteColumn(col)" class="btn-delete" title="Xóa cột">✕</button>
                    </div>
                  </div>
                }
              </div>
            }
          </div>

          <!-- Add Column Box -->
          <div class="card add-col-card">
            <h3>+ Thêm Cột vào Board</h3>
            <p class="hint">Liên kết cột với một Trạng thái trong dự án và thiết lập giới hạn WIP.</p>

            <form (ngSubmit)="onCreateColumn()">
              <div class="form-group">
                <label for="colName">Tên hiển thị của Cột</label>
                <input
                  type="text"
                  id="colName"
                  name="colName"
                  [(ngModel)]="newColumn.name"
                  placeholder="Để trống sẽ lấy tên trạng thái"
                  class="form-control"
                />
              </div>

              <div class="form-group">
                <label for="statusId">Trạng thái liên kết (Status) <span class="required">*</span></label>
                <select
                  id="statusId"
                  name="statusId"
                  [(ngModel)]="newColumn.statusId"
                  required
                  class="form-control"
                >
                  <option value="" disabled>-- Chọn trạng thái map vào cột --</option>
                  @for (s of statuses(); track s.id) {
                    <option [value]="s.id">{{ s.name }} ({{ s.category }})</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="wipLimit">Giới hạn công việc (WIP Limit)</label>
                <input
                  type="number"
                  id="wipLimit"
                  name="wipLimit"
                  [(ngModel)]="newColumn.wipLimit"
                  min="0"
                  placeholder="VD: 3 hoặc 5 (để trống = vô hạn)"
                  class="form-control"
                />
                <small class="form-hint">Chặn nhập số âm (&lt; 0). Cảnh báo khi số task vượt quá giới hạn này.</small>
              </div>

              <button
                type="submit"
                [disabled]="submittingCol() || !newColumn.statusId"
                class="btn btn-primary btn-block"
              >
                {{ submittingCol() ? 'Đang thêm cột...' : '+ Thêm Cột' }}
              </button>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .board-settings-container {
      padding: 2rem;
      max-width: 1150px;
      margin: 0 auto;
    }
    .breadcrumb {
      font-size: 0.85rem;
      color: #64748b;
      margin-bottom: 1rem;
    }
    .breadcrumb a {
      color: #2563eb;
      text-decoration: none;
    }
    h1 {
      font-size: 1.6rem;
      font-weight: 700;
      color: #1e293b;
      margin: 0;
    }
    .subtitle {
      color: #64748b;
      font-size: 0.9rem;
      margin-top: 0.25rem;
    }
    .settings-tabs {
      display: flex;
      gap: 0.5rem;
      border-bottom: 2px solid #e2e8f0;
      margin: 1.5rem 0;
    }
    .tab-item {
      padding: 0.75rem 1.25rem;
      font-weight: 600;
      font-size: 0.95rem;
      color: #64748b;
      text-decoration: none;
      border-bottom: 2px solid transparent;
      margin-bottom: -2px;
      cursor: pointer;
    }
    .tab-item:hover {
      color: #2563eb;
    }
    .tab-item.active {
      color: #2563eb;
      border-bottom-color: #2563eb;
    }
    .card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.5rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      margin-bottom: 1.5rem;
    }
    .board-select-box {
      border-left: 4px solid #2563eb;
    }
    .board-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
    }
    .board-tabs {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
      margin-top: 0.75rem;
    }
    .board-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.5rem 1rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      background: #f8fafc;
      color: #334155;
      cursor: pointer;
      font-size: 0.9rem;
      transition: all 0.2s;
    }
    .board-btn:hover {
      background: #f1f5f9;
      border-color: #94a3b8;
    }
    .board-btn.active {
      background: #eff6ff;
      border-color: #2563eb;
      color: #1d4ed8;
      box-shadow: 0 0 0 2px rgba(37,99,235,0.2);
    }
    .board-type-tag {
      font-size: 0.75rem;
      padding: 0.15rem 0.4rem;
      border-radius: 4px;
      background: #e2e8f0;
      font-weight: 600;
    }
    .default-pill {
      font-size: 0.7rem;
      padding: 0.1rem 0.4rem;
      border-radius: 10px;
      background: #dcfce7;
      color: #15803d;
      font-weight: 600;
    }
    .new-board-form {
      margin-top: 1rem;
      padding-top: 1rem;
      border-top: 1px dashed #e2e8f0;
    }
    .new-board-form h4 {
      margin: 0 0 0.5rem 0;
      font-size: 0.95rem;
      color: #1e293b;
    }
    .form-row {
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
    .columns-management-grid {
      display: grid;
      grid-template-columns: 1fr 340px;
      gap: 1.5rem;
    }
    h3 {
      font-size: 1.15rem;
      font-weight: 600;
      color: #0f172a;
      margin: 0 0 0.25rem 0;
    }
    .hint {
      color: #64748b;
      font-size: 0.85rem;
      margin: 0 0 1rem 0;
    }
    .column-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .column-item {
      display: flex;
      align-items: center;
      padding: 0.85rem 1rem;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      gap: 1rem;
      transition: all 0.2s;
    }
    .column-item:hover {
      border-color: #cbd5e1;
      background: #ffffff;
      box-shadow: 0 2px 4px rgba(0,0,0,0.03);
    }
    .col-drag-handle {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.2rem;
    }
    .btn-arrow {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      padding: 0.15rem 0.4rem;
      font-size: 0.7rem;
      cursor: pointer;
      color: #475569;
    }
    .btn-arrow:hover:not(:disabled) {
      background: #e2e8f0;
      color: #0f172a;
    }
    .btn-arrow:disabled {
      opacity: 0.3;
      cursor: not-allowed;
    }
    .order-idx {
      font-size: 0.75rem;
      font-weight: 700;
      color: #64748b;
    }
    .col-info {
      flex-grow: 1;
    }
    .col-title {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.25rem;
    }
    .status-linked {
      font-size: 0.8rem;
      background: #f1f5f9;
      color: #475569;
      padding: 0.15rem 0.5rem;
      border-radius: 10px;
    }
    .wip-badge {
      font-size: 0.8rem;
      padding: 0.15rem 0.5rem;
      border-radius: 4px;
      background: #fef3c7;
      color: #92400e;
    }
    .wip-unlimited {
      background: #f1f5f9;
      color: #94a3b8;
    }
    .btn-delete {
      background: transparent;
      border: none;
      color: #ef4444;
      font-size: 1.1rem;
      cursor: pointer;
      padding: 0.3rem 0.5rem;
      border-radius: 4px;
    }
    .btn-delete:hover {
      background: #fee2e2;
    }
    .form-group {
      margin-bottom: 1rem;
    }
    label {
      display: block;
      font-weight: 600;
      color: #334155;
      font-size: 0.85rem;
      margin-bottom: 0.3rem;
    }
    .required {
      color: #ef4444;
    }
    .form-control {
      width: 100%;
      padding: 0.6rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      font-size: 0.9rem;
      box-sizing: border-box;
    }
    .form-control:focus {
      outline: none;
      border-color: #2563eb;
      box-shadow: 0 0 0 3px rgba(37,99,235,0.15);
    }
    .form-hint {
      display: block;
      color: #64748b;
      font-size: 0.75rem;
      margin-top: 0.25rem;
    }
    .btn {
      padding: 0.6rem 1.2rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.9rem;
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 0.2s;
    }
    .btn-primary {
      background: #2563eb;
      color: #ffffff;
    }
    .btn-primary:hover:not(:disabled) {
      background: #1d4ed8;
    }
    .btn-secondary {
      background: #f1f5f9;
      color: #334155;
      border-color: #cbd5e1;
    }
    .btn-sm {
      padding: 0.35rem 0.75rem;
      font-size: 0.85rem;
    }
    .btn-block {
      width: 100%;
      margin-top: 0.5rem;
    }
    .alert {
      padding: 0.85rem 1rem;
      border-radius: 8px;
      margin-bottom: 1.25rem;
      font-size: 0.9rem;
    }
    .alert-error {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #991b1b;
    }
    .alert-success {
      background: #f0fdf4;
      border: 1px solid #bbf7d0;
      color: #166534;
    }
    .spinner {
      width: 32px;
      height: 32px;
      border: 3px solid #e2e8f0;
      border-top-color: #2563eb;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 0.75rem auto;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    .loading-state, .empty-columns {
      text-align: center;
      padding: 2.5rem;
      color: #64748b;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BoardSettingsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly projectService = inject(ProjectManagementService);
  private readonly confirmService = inject(ConfirmDialogService);
  private readonly toastService = inject(ToastService);

  projectId = '';
  readonly project = signal<Project | null>(null);
  readonly boards = signal<Board[]>([]);
  readonly selectedBoard = signal<Board | null>(null);
  readonly columns = signal<BoardColumn[]>([]);
  readonly statuses = signal<ProjectStatus[]>([]);

  readonly showNewBoardModal = signal<boolean>(false);
  readonly columnsLoading = signal<boolean>(false);
  readonly submittingCol = signal<boolean>(false);
  readonly alertMessage = signal<string | null>(null);
  readonly alertType = signal<'success' | 'error'>('success');

  newBoard: CreateBoardRequest = {
    name: '',
    type: 'Kanban',
    isDefault: false
  };

  newColumn: CreateBoardColumnRequest = {
    statusId: '',
    name: '',
    wipLimit: null
  };

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id') || '';
    if (this.projectId) {
      this.loadProject();
      this.loadStatuses();
      this.loadBoards();
    }
  }

  loadProject(): void {
    this.projectService.getProjectById(this.projectId).subscribe(p => this.project.set(p));
  }

  loadStatuses(): void {
    this.projectService.getProjectStatuses(this.projectId).subscribe(s => this.statuses.set(s));
  }

  loadBoards(): void {
    this.projectService.getBoards(this.projectId).subscribe({
      next: (bList) => {
        this.boards.set(bList);
        if (bList.length > 0 && !this.selectedBoard()) {
          const defaultB = bList.find(x => x.isDefault) || bList[0];
          this.selectBoard(defaultB);
        }
      }
    });
  }

  selectBoard(b: Board): void {
    this.selectedBoard.set(b);
    this.loadColumns(b.id);
  }

  loadColumns(boardId: string): void {
    this.columnsLoading.set(true);
    this.projectService.getBoardColumns(boardId).subscribe({
      next: (cols) => {
        this.columns.set(cols.sort((a, b) => a.orderIndex - b.orderIndex));
        this.columnsLoading.set(false);
      },
      error: () => this.columnsLoading.set(false)
    });
  }

  onCreateBoard(): void {
    if (!this.newBoard.name.trim()) return;

    this.projectService.createBoard(this.projectId, this.newBoard).subscribe({
      next: (b) => {
        this.showNewBoardModal.set(false);
        this.newBoard = { name: '', type: 'Kanban', isDefault: false };
        this.alertType.set('success');
        this.alertMessage.set(`Đã tạo Board "${b.name}" thành công!`);
        this.toastService.success('Tạo Board', `Đã tạo Bảng mới "${b.name}"`);
        this.loadBoards();
        this.selectBoard(b);
      },
      error: (err) => {
        this.alertType.set('error');
        this.alertMessage.set(err?.error?.title || 'Không thể tạo Board.');
        this.toastService.error('Lỗi', err?.error?.title || 'Không thể tạo Board.');
      }
    });
  }

  onCreateColumn(): void {
    const board = this.selectedBoard();
    if (!board) return;

    // 1. Chặn WIP < 0
    if (this.newColumn.wipLimit !== null && this.newColumn.wipLimit !== undefined && this.newColumn.wipLimit < 0) {
      this.alertType.set('error');
      this.alertMessage.set('Lỗi: WIP limit không được là số âm (< 0)!');
      this.toastService.error('Lỗi WIP Limit', 'Giới hạn WIP không được là số âm (< 0)');
      return;
    }

    this.submittingCol.set(true);
    this.alertMessage.set(null);

    this.projectService.createBoardColumn(board.id, this.newColumn).subscribe({
      next: (col) => {
        this.submittingCol.set(false);
        this.alertType.set('success');
        this.alertMessage.set(`Đã thêm cột "${col.name}" vào Board!`);
        this.toastService.success('Thêm Cột Board', `Đã thêm cột "${col.name}"`);
        this.newColumn = { statusId: '', name: '', wipLimit: null };
        this.loadColumns(board.id);
      },
      error: (err) => {
        this.submittingCol.set(false);
        this.alertType.set('error');
        this.alertMessage.set(err?.error?.title || 'Không thể thêm cột.');
        this.toastService.error('Lỗi', err?.error?.title || 'Không thể thêm cột.');
      }
    });
  }

  moveColumn(index: number, direction: -1 | 1): void {
    const cols = [...this.columns()];
    const targetIdx = index + direction;
    if (targetIdx < 0 || targetIdx >= cols.length) return;

    // Swap
    const temp = cols[index];
    cols[index] = cols[targetIdx];
    cols[targetIdx] = temp;

    // Update locally
    this.columns.set(cols);

    // Save order to server
    const orderedIds = cols.map(c => c.id);
    const board = this.selectedBoard();
    if (board) {
      this.projectService.reorderBoardColumns(board.id, orderedIds).subscribe({
        next: () => {
          this.alertType.set('success');
          this.alertMessage.set('Đã lưu thứ tự cột thành công!');
          this.toastService.success('Cập Nhật Thứ Tự', 'Đã sắp xếp lại thứ tự cột trên bảng');
          this.loadColumns(board.id);
        },
        error: (err) => {
          this.alertType.set('error');
          this.alertMessage.set(err?.error?.title || 'Không thể lưu thứ tự cột.');
        }
      });
    }
  }

  onDeleteColumn(col: BoardColumn): void {
    this.confirmService.confirm({
      title: 'Xóa Cột Board',
      message: `Bạn có chắc chắn muốn xóa cột "${col.name || col.statusName}" khỏi Bảng không?`,
      type: 'warning',
      confirmText: 'Xóa cột ngay',
      cancelText: 'Hủy bỏ',
      onConfirm: () => {
        this.projectService.deleteBoardColumn(col.id).subscribe({
          next: () => {
            this.alertType.set('success');
            this.alertMessage.set(`Đã xóa cột "${col.name}".`);
            this.toastService.warning('Đã Xóa Cột', `Đã loại bỏ cột "${col.name}" khỏi bảng`);
            if (this.selectedBoard()) {
              this.loadColumns(this.selectedBoard()!.id);
            }
          },
          error: (err) => {
            this.alertType.set('error');
            this.alertMessage.set(err?.error?.title || 'Không thể xóa cột.');
            this.toastService.error('Lỗi Xóa Cột', err?.error?.title || 'Không thể xóa cột.');
          }
        });
      }
    });
  }
}
