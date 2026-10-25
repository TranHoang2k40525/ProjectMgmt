import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  ProjectManagementService,
  SpecDocument
} from '../../core/services/project-management.service';
import { IdentityService } from '../../core/services/identity.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';

@Component({
  selector: 'app-spec-hub-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './spec-hub-page.html',
  styleUrl: './spec-hub-page.scss'
})
export class SpecHubPageComponent implements OnInit {
  readonly projectService = inject(ProjectManagementService);
  private readonly identityService = inject(IdentityService);
  private readonly toastService = inject(ToastService);
  private readonly confirmService = inject(ConfirmDialogService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly activeProject = this.projectService.currentProject;
  readonly documentsList = signal<SpecDocument[]>([]);
  readonly selectedDoc = signal<SpecDocument | null>(null);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);

  // Chế độ xem & sửa
  readonly isEditing = signal(false);
  readonly editTitle = signal('');
  readonly editContent = signal('');

  // Modal tạo tài liệu mới
  readonly isCreateModalOpen = signal(false);
  readonly newDocTitle = signal('');
  readonly newDocContent = signal('');

  // Modal bóc tách User Story
  readonly isExtractModalOpen = signal(false);
  readonly extractStoryTitle = signal('');
  readonly extractExcerpt = signal('');
  readonly extractStoryPoints = signal<number>(3);
  readonly extractCriteria = signal<string[]>(['']);

  // Bình luận & phản biện
  readonly activeTab = signal<'preview' | 'stories' | 'comments'>('preview');
  readonly newCommentText = signal('');
  readonly selectedHighlightText = signal('');

  // Quyền hạn Scrum
  readonly isProjectLeadOrOwner = computed(() => {
    const currentUserId = this.identityService.authState().currentUser?.id;
    const proj = this.activeProject();
    if (!proj || !currentUserId) return false;
    return proj.leadUserId === currentUserId;
  });

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const docId = params.get('docId');
      this.loadDocuments(docId);
    });
  }

  loadDocuments(autoSelectDocId?: string | null): void {
    const projectId = this.activeProject()?.id;
    if (!projectId) return;

    this.isLoading.set(true);
    this.projectService.getProjectDocuments(projectId).subscribe({
      next: docs => {
        this.documentsList.set(docs);
        this.isLoading.set(false);

        if (autoSelectDocId) {
          this.selectDocumentById(autoSelectDocId);
        } else if (docs.length > 0 && !this.selectedDoc()) {
          this.selectDocumentById(docs[0].id);
        }
      },
      error: () => {
        this.isLoading.set(false);
        this.toastService.error('Đặc tả', 'Không thể nạp danh sách tài liệu đặc tả.');
      }
    });
  }

  selectDocumentById(docId: string): void {
    this.isLoading.set(true);
    this.projectService.getDocument(docId).subscribe({
      next: doc => {
        this.selectedDoc.set(doc);
        this.editTitle.set(doc.title);
        this.editContent.set(doc.content ?? '');
        this.isEditing.set(false);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toastService.error('Đặc tả', 'Không thể mở tài liệu đặc tả.');
      }
    });
  }

  openCreateModal(): void {
    this.newDocTitle.set('');
    this.newDocContent.set(
      '# 1. Mục tiêu (Objective)\nMô tả mục tiêu kinh doanh và giá trị mang lại cho người dùng theo triết lý Agile Scrum.\n\n# 2. Phạm vi tính năng (Scope)\n- Tính năng A\n- Tính năng B\n\n# 3. Yêu cầu chi tiết (Detailed Specifications)\nMô tả chi tiết các luồng nghiệp vụ...\n\n# 4. Tiêu chí thành công (Acceptance Criteria)\n- Điều kiện 1\n- Điều kiện 2'
    );
    this.isCreateModalOpen.set(true);
  }

  closeCreateModal(): void {
    this.isCreateModalOpen.set(false);
  }

  submitCreateDocument(): void {
    const projectId = this.activeProject()?.id;
    const title = this.newDocTitle().trim();
    if (!projectId || !title) return;

    this.isSaving.set(true);
    this.projectService.createProjectDocument(projectId, title, this.newDocContent()).subscribe({
      next: res => {
        this.isSaving.set(false);
        this.isCreateModalOpen.set(false);
        this.toastService.success('Đặc tả', `Đã tạo tài liệu "${title}" thành công.`);
        this.loadDocuments(res.id);
      },
      error: () => {
        this.isSaving.set(false);
        this.toastService.error('Đặc tả', 'Lỗi khi tạo tài liệu mới.');
      }
    });
  }

  startEditing(): void {
    const doc = this.selectedDoc();
    if (!doc) return;
    this.editTitle.set(doc.title);
    this.editContent.set(doc.content ?? '');
    this.isEditing.set(true);
  }

  cancelEditing(): void {
    this.isEditing.set(false);
  }

  saveDocumentEdits(): void {
    const doc = this.selectedDoc();
    if (!doc) return;

    const title = this.editTitle().trim();
    if (!title) {
      this.toastService.warning('Đặc tả', 'Tiêu đề tài liệu không được để trống.');
      return;
    }

    this.isSaving.set(true);
    this.projectService.updateProjectDocument(doc.id, title, this.editContent()).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.isEditing.set(false);
        this.toastService.success('Đặc tả', 'Đã lưu tài liệu đặc tả thành công.');
        this.selectDocumentById(doc.id);
      },
      error: () => {
        this.isSaving.set(false);
        this.toastService.error('Đặc tả', 'Lỗi khi lưu tài liệu.');
      }
    });
  }

  changeDocumentStatus(status: 'Draft' | 'Review' | 'Approved'): void {
    const doc = this.selectedDoc();
    if (!doc) return;

    if (status === 'Approved' && !this.isProjectLeadOrOwner()) {
      this.toastService.warning('Phân quyền', 'Chỉ Giám đốc dự án / PO mới có quyền phê duyệt tài liệu đặc tả.');
      return;
    }

    this.projectService.updateProjectDocumentStatus(doc.id, status).subscribe({
      next: () => {
        this.toastService.success('Đặc tả', `Đã cập nhật trạng thái tài liệu sang ${status}.`);
        this.selectDocumentById(doc.id);
      },
      error: () => {
        this.toastService.error('Đặc tả', 'Không thể cập nhật trạng thái tài liệu.');
      }
    });
  }

  deleteCurrentDocument(): void {
    const doc = this.selectedDoc();
    if (!doc) return;

    this.confirmService.confirm({
      title: 'Xóa tài liệu đặc tả',
      message: `Bạn có chắc chắn muốn xóa tài liệu "${doc.title}"? Thao tác này không thể hoàn tác.`,
      confirmText: 'Xóa tài liệu',
      cancelText: 'Hủy',
      type: 'danger',
      onConfirm: () => {
        this.projectService.deleteProjectDocument(doc.id).subscribe({
          next: () => {
            this.toastService.success('Đặc tả', 'Đã xóa tài liệu đặc tả.');
            this.selectedDoc.set(null);
            this.loadDocuments();
          },
          error: () => {
            this.toastService.error('Đặc tả', 'Lỗi khi xóa tài liệu.');
          }
        });
      }
    });
  }

  // --- BÓC TÁCH USER STORY TRỰC TIẾP TỪ NỘI DUNG ĐẶC TẢ ---
  onTextSelection(): void {
    const selection = window.getSelection();
    if (selection && selection.toString().trim().length > 0) {
      const selectedText = selection.toString().trim();
      this.selectedHighlightText.set(selectedText);
    }
  }

  openExtractStoryModal(): void {
    const selectionText = this.selectedHighlightText() || window.getSelection()?.toString().trim() || '';
    this.extractExcerpt.set(selectionText);
    
    // Gợi ý tiêu đề từ câu đầu tiên của excerpt
    const firstSentence = selectionText.split('\n')[0].replace(/^[#-*\s]+/, '').trim();
    this.extractStoryTitle.set(firstSentence ? (firstSentence.length > 80 ? firstSentence.slice(0, 77) + '...' : firstSentence) : 'Người dùng muốn...');
    this.extractStoryPoints.set(3);
    this.extractCriteria.set(['']);
    this.isExtractModalOpen.set(true);
  }

  closeExtractStoryModal(): void {
    this.isExtractModalOpen.set(false);
  }

  addCriterion(): void {
    this.extractCriteria.update(list => [...list, '']);
  }

  removeCriterion(index: number): void {
    this.extractCriteria.update(list => list.filter((_, i) => i !== index));
  }

  updateCriterion(index: number, val: string): void {
    this.extractCriteria.update(list => {
      const copy = [...list];
      copy[index] = val;
      return copy;
    });
  }

  submitExtractStory(): void {
    const doc = this.selectedDoc();
    const title = this.extractStoryTitle().trim();
    if (!doc || !title) {
      this.toastService.warning('Bóc tách Story', 'Vui lòng nhập tiêu đề cho User Story.');
      return;
    }

    const ac = this.extractCriteria().map(c => c.trim()).filter(c => c.length > 0);
    this.isSaving.set(true);
    this.projectService.extractStoryFromDoc(doc.id, title, this.extractExcerpt(), this.extractStoryPoints(), ac).subscribe({
      next: res => {
        this.isSaving.set(false);
        this.isExtractModalOpen.set(false);
        this.toastService.success('Bóc tách Story', `Đã tạo thành công User Story ${res.issueKey}: ${res.title}`);
        // Tải lại tài liệu để cập nhật linked stories
        this.selectDocumentById(doc.id);
        // Đồng thời cập nhật lại Backlog workspace
        this.projectService.loadWorkspace();
      },
      error: () => {
        this.isSaving.set(false);
        this.toastService.error('Bóc tách Story', 'Không thể bóc tách Story từ tài liệu.');
      }
    });
  }

  // --- BÌNH LUẬN & PHẢN BIỆN ---
  submitComment(): void {
    const doc = this.selectedDoc();
    const text = this.newCommentText().trim();
    if (!doc || !text) return;

    const highlight = this.selectedHighlightText();
    this.projectService.addDocumentComment(doc.id, text, highlight || undefined).subscribe({
      next: () => {
        this.toastService.success('Phản biện', 'Đã gửi ý kiến phản biện.');
        this.newCommentText.set('');
        this.selectedHighlightText.set('');
        this.selectDocumentById(doc.id);
      },
      error: () => {
        this.toastService.error('Phản biện', 'Không thể gửi phản biện.');
      }
    });
  }
}
