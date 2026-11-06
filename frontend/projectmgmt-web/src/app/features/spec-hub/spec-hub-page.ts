import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { DomSanitizer, SafeResourceUrl, SafeHtml } from '@angular/platform-browser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  ProjectManagementService,
  SpecTreeNode,
  SpecFileContent
} from '../../core/services/project-management.service';
import { IdentityService } from '../../core/services/identity.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';

export interface OpenSpecTab {
  id: string;
  name: string;
  extension?: string;
  fileType: 'text' | 'pdf' | 'docx' | 'xlsx' | 'image' | 'markdown';
  content?: string;
  previewData?: any;
  status: 'Draft' | 'Review' | 'Approved';
  version: number;
  isLoading?: boolean;
  isSaving?: boolean;
  isDirty?: boolean;
  rawUrl: string;
  downloadUrl: string;
  safePdfUrl?: SafeResourceUrl;
  activeSheetIndex?: number;
  markdownMode?: 'view' | 'edit';
}

@Component({
  selector: 'app-spec-hub-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './spec-hub-page.html',
  styleUrl: './spec-hub-page.scss'
})
export class SpecHubPageComponent implements OnInit {
  readonly projectService = inject(ProjectManagementService);
  private readonly identityService = inject(IdentityService);
  private readonly toastService = inject(ToastService);
  private readonly confirmService = inject(ConfirmDialogService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly activeProject = this.projectService.currentProject;
  readonly treeNodes = signal<SpecTreeNode[]>([]);
  readonly isLoadingTree = signal(false);

  // Thư mục đang mở rộng
  readonly expandedFolderIds = signal<Set<string>>(new Set());
  readonly selectedNodeId = signal<string | null>(null);

  // Tabs mở nhiều file (VS Code style)
  readonly openTabs = signal<OpenSpecTab[]>([]);
  readonly activeTabId = signal<string | null>(null);

  readonly currentTab = computed(() => {
    const id = this.activeTabId();
    if (!id) return null;
    return this.openTabs().find(t => t.id === id) ?? null;
  });

  // Quyền hạn Scrum (Chỉ Lead/Owner/PM/SM được tạo/sửa/xóa)
  readonly isManager = computed(() => {
    if (this.identityService.isSystemAdmin()) return true;
    const currentUserId = this.identityService.authState().currentUser?.id;
    const proj = this.activeProject();
    if (!proj || !currentUserId) return false;
    if (proj.leadUserId === currentUserId) return true;
    const userMember = proj.members?.find(m => m.userId === currentUserId);
    if (!userMember) return false;
    const roleLower = userMember.role.toLowerCase();
    return (
      roleLower.includes('lead') ||
      roleLower.includes('manager') ||
      roleLower.includes('master') ||
      roleLower.includes('owner')
    );
  });

  // Modal Tạo thư mục
  readonly isCreateFolderModalOpen = signal(false);
  readonly newFolderName = signal('');
  readonly folderParentId = signal<string | null>(null);

  // Modal Tạo file văn bản (.txt)
  readonly isCreateTextFileModalOpen = signal(false);
  readonly newTextFileName = signal('');
  readonly newTextFileContent = signal('');
  readonly textFileParentId = signal<string | null>(null);

  // Modal Đổi tên
  readonly isRenameModalOpen = signal(false);
  readonly renameItemId = signal<string | null>(null);
  readonly renameItemName = signal('');

  // Modal Bóc tách Story
  readonly isExtractModalOpen = signal(false);
  readonly extractStoryTitle = signal('');
  readonly extractExcerpt = signal('');
  readonly extractStoryPoints = signal<number>(3);
  readonly extractCriteria = signal<string[]>(['']);

  // Phản biện & Tab chi tiết bên phải
  readonly isSidePanelOpen = signal(false);
  readonly activeSideTab = signal<'comments' | 'stories'>('comments');
  readonly newCommentText = signal('');
  readonly selectedHighlightText = signal('');

  // Target tải lên file
  uploadTargetParentId: string | null = null;

  // Drag and Drop State
  readonly draggedNode = signal<SpecTreeNode | null>(null);
  readonly dragOverFolderId = signal<string | null>(null);

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const docId = params.get('docId');
      this.loadTree(docId);
    });
  }

  loadTree(autoOpenDocId?: string | null): void {
    const projectId = this.activeProject()?.id;
    if (!projectId) return;

    this.isLoadingTree.set(true);
    this.projectService.getSpecTree(projectId).subscribe({
      next: nodes => {
        this.treeNodes.set(nodes);
        this.isLoadingTree.set(false);

        // Mặc định mở rộng cấp đầu tiên nếu có thư mục
        const folderIds = new Set<string>();
        nodes.forEach(n => {
          if (n.isDirectory) folderIds.add(n.id);
        });
        this.expandedFolderIds.set(folderIds);

        if (autoOpenDocId) {
          const targetNode = this.findNodeInTree(nodes, autoOpenDocId);
          if (targetNode && !targetNode.isDirectory) {
            this.openTabForNode(targetNode);
          }
        } else if (this.openTabs().length === 0) {
          // Mở tệp đầu tiên tìm thấy
          const firstFile = this.findFirstFile(nodes);
          if (firstFile) {
            this.openTabForNode(firstFile);
          }
        }
      },
      error: () => {
        this.isLoadingTree.set(false);
        this.toastService.error('Spec Hub', 'Không thể nạp cây thư mục tài liệu.');
      }
    });
  }

  private findNodeInTree(nodes: SpecTreeNode[], id: string): SpecTreeNode | null {
    for (const node of nodes) {
      if (node.id === id) return node;
      if (node.children?.length) {
        const found = this.findNodeInTree(node.children, id);
        if (found) return found;
      }
    }
    return null;
  }

  private findFirstFile(nodes: SpecTreeNode[]): SpecTreeNode | null {
    for (const node of nodes) {
      if (!node.isDirectory) return node;
      if (node.children?.length) {
        const f = this.findFirstFile(node.children);
        if (f) return f;
      }
    }
    return null;
  }

  // --- CÂY THƯ MỤC: MỞ RỘNG / THU GỌN ---
  toggleFolder(node: SpecTreeNode, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.expandedFolderIds.update(set => {
      const copy = new Set(set);
      if (copy.has(node.id)) {
        copy.delete(node.id);
      } else {
        copy.add(node.id);
      }
      return copy;
    });
    this.selectedNodeId.set(node.id);
  }

  isFolderExpanded(id: string): boolean {
    return this.expandedFolderIds().has(id);
  }

  collapseAllFolders(): void {
    this.expandedFolderIds.set(new Set());
  }

  // --- MỞ TỆP TIN TRONG MULTI-TAB WORKSPACE ---
  onNodeClick(node: SpecTreeNode): void {
    this.selectedNodeId.set(node.id);
    if (node.isDirectory) {
      this.toggleFolder(node);
    } else {
      this.openTabForNode(node);
    }
  }

  openTabForNode(node: SpecTreeNode): void {
    const existing = this.openTabs().find(t => t.id === node.id);
    if (existing) {
      this.activeTabId.set(node.id);
      return;
    }

    const ext = (node.extension ?? '').toLowerCase();
    let fileType: 'text' | 'pdf' | 'docx' | 'xlsx' | 'image' | 'markdown' = 'text';
    if (ext === '.pdf') fileType = 'pdf';
    else if (ext === '.docx') fileType = 'docx';
    else if (ext === '.xlsx') fileType = 'xlsx';
    else if (['.png', '.jpg', '.jpeg', '.gif', '.webp', '.svg', '.bmp'].includes(ext)) fileType = 'image';
    else if (ext === '.md') fileType = 'markdown';

    const rawUrl = this.projectService.getRawFileUrl(node.id);
    const downloadUrl = this.projectService.getDownloadUrl(node.id);
    const safePdfUrl = fileType === 'pdf' ? this.sanitizer.bypassSecurityTrustResourceUrl(rawUrl) : undefined;

    const newTab: OpenSpecTab = {
      id: node.id,
      name: node.name,
      extension: ext,
      fileType,
      status: node.status,
      version: node.version,
      isLoading: fileType !== 'image',
      rawUrl,
      downloadUrl,
      safePdfUrl,
      activeSheetIndex: 0,
      markdownMode: 'view'
    };

    this.openTabs.update(tabs => [...tabs, newTab]);
    this.activeTabId.set(node.id);

    // Tải nội dung chi tiết
    this.projectService.getFileContent(node.id).subscribe({
      next: content => {
        this.openTabs.update(tabs =>
          tabs.map(t => {
            if (t.id === node.id) {
              return {
                ...t,
                content: content.content ?? '',
                previewData: content.previewData,
                status: content.status,
                version: content.version,
                isLoading: false,
                isDirty: false
              };
            }
            return t;
          })
        );
      },
      error: () => {
        this.openTabs.update(tabs =>
          tabs.map(t => (t.id === node.id ? { ...t, isLoading: false } : t))
        );
        this.toastService.error('Tệp tin', `Không thể nạp nội dung của tệp "${node.name}".`);
      }
    });
  }

  selectTab(tabId: string): void {
    this.activeTabId.set(tabId);
    this.selectedNodeId.set(tabId);
  }

  closeTab(tabId: string, event?: MouseEvent): void {
    if (event) event.stopPropagation();

    const tabs = this.openTabs();
    const index = tabs.findIndex(t => t.id === tabId);
    if (index === -1) return;

    const updated = tabs.filter(t => t.id !== tabId);
    this.openTabs.set(updated);

    if (this.activeTabId() === tabId) {
      if (updated.length > 0) {
        const nextIndex = Math.min(index, updated.length - 1);
        this.activeTabId.set(updated[nextIndex].id);
      } else {
        this.activeTabId.set(null);
      }
    }
  }

  // --- LƯU THAY ĐỔI CHO FILE TEXT / MARKDOWN ---
  onTextChange(newContent: string): void {
    const tab = this.currentTab();
    if (!tab || (tab.fileType !== 'text' && tab.fileType !== 'markdown')) return;

    this.openTabs.update(tabs =>
      tabs.map(t => (t.id === tab.id ? { ...t, content: newContent, isDirty: true } : t))
    );
  }

  saveActiveTextFile(): void {
    const tab = this.currentTab();
    if (!tab || (tab.fileType !== 'text' && tab.fileType !== 'markdown') || !this.isManager()) return;

    this.openTabs.update(tabs =>
      tabs.map(t => (t.id === tab.id ? { ...t, isSaving: true } : t))
    );

    this.projectService.updateTextFileContent(tab.id, tab.content ?? '').subscribe({
      next: res => {
        this.openTabs.update(tabs =>
          tabs.map(t =>
            t.id === tab.id
              ? { ...t, isSaving: false, isDirty: false, version: res.version ?? t.version + 1 }
              : t
          )
        );
        this.toastService.success('Đặc tả', `Đã lưu tệp "${tab.name}" thành công.`);
        this.loadTree(tab.id);
      },
      error: () => {
        this.openTabs.update(tabs =>
          tabs.map(t => (t.id === tab.id ? { ...t, isSaving: false } : t))
        );
        this.toastService.error('Đặc tả', 'Lỗi khi lưu tệp văn bản.');
      }
    });
  }

  // --- CẬP NHẬT TRẠNG THÁI DUYỆT (APPROVED / REVIEW / DRAFT) ---
  changeTabStatus(status: 'Draft' | 'Review' | 'Approved'): void {
    const tab = this.currentTab();
    if (!tab) return;

    if (status === 'Approved' && !this.isManager()) {
      this.toastService.warning('Phân quyền', 'Chỉ Giám đốc dự án / PO mới có quyền phê duyệt tài liệu.');
      return;
    }

    this.projectService.updateProjectDocumentStatus(tab.id, status).subscribe({
      next: () => {
        this.openTabs.update(tabs =>
          tabs.map(t => (t.id === tab.id ? { ...t, status } : t))
        );
        this.toastService.success('Đặc tả', `Đã cập nhật trạng thái sang "${status}".`);
        this.loadTree(tab.id);
      },
      error: () => {
        this.toastService.error('Đặc tả', 'Không thể cập nhật trạng thái tài liệu.');
      }
    });
  }

  // --- TẢI XUỐNG FILE GỐC ---
  downloadFile(id?: string): void {
    const targetId = id ?? this.activeTabId();
    if (!targetId) return;
    const url = this.projectService.getDownloadUrl(targetId);
    window.open(url, '_blank');
  }

  // --- CHỌN SHEET CHO EXCEL ---
  selectExcelSheet(index: number): void {
    const tab = this.currentTab();
    if (!tab) return;
    this.openTabs.update(tabs =>
      tabs.map(t => (t.id === tab.id ? { ...t, activeSheetIndex: index } : t))
    );
  }

  // --- MODAL: TẠO THƯ MỤC ---
  openCreateFolderModal(parentId?: string | null, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.folderParentId.set(parentId ?? null);
    this.newFolderName.set('');
    this.isCreateFolderModalOpen.set(true);
  }

  submitCreateFolder(): void {
    const projectId = this.activeProject()?.id;
    const name = this.newFolderName().trim();
    if (!projectId || !name) return;

    this.projectService.createFolder(projectId, name, this.folderParentId()).subscribe({
      next: res => {
        this.isCreateFolderModalOpen.set(false);
        this.toastService.success('Thư mục', `Đã tạo thư mục "${name}" thành công.`);
        this.loadTree();
        if (this.folderParentId()) {
          this.expandedFolderIds.update(s => new Set(s).add(this.folderParentId()!));
        }
      },
      error: () => {
        this.toastService.error('Thư mục', 'Lỗi khi tạo thư mục mới.');
      }
    });
  }

  // --- MODAL: TẠO FILE VĂN BẢN (.TXT) ---
  openCreateTextFileModal(parentId?: string | null, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.textFileParentId.set(parentId ?? null);
    this.newTextFileName.set('');
    this.newTextFileContent.set(
      '# 1. Mục tiêu (Objective)\nMô tả mục tiêu kỹ thuật...\n\n# 2. Tiêu chí nghiệm thu (Acceptance Criteria)\n- Tiêu chí 1\n- Tiêu chí 2'
    );
    this.isCreateTextFileModalOpen.set(true);
  }

  submitCreateTextFile(): void {
    const projectId = this.activeProject()?.id;
    let name = this.newTextFileName().trim();
    if (!projectId || !name) return;

    if (!name.includes('.')) name += '.txt';

    this.projectService.createTextFile(projectId, name, this.newTextFileContent(), this.textFileParentId()).subscribe({
      next: res => {
        this.isCreateTextFileModalOpen.set(false);
        this.toastService.success('Tệp tin', `Đã tạo tệp "${name}" thành công.`);
        this.loadTree(res.id);
        if (this.textFileParentId()) {
          this.expandedFolderIds.update(s => new Set(s).add(this.textFileParentId()!));
        }
      },
      error: () => {
        this.toastService.error('Tệp tin', 'Lỗi khi tạo tệp văn bản mới.');
      }
    });
  }

  // --- TẢI TỆP LÊN (UPLOAD) ---
  triggerFileUpload(parentId?: string | null, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.uploadTargetParentId = parentId ?? null;
    const input = document.getElementById('spec-file-input') as HTMLInputElement;
    if (input) {
      input.value = '';
      input.click();
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const file = input.files[0];
    const projectId = this.activeProject()?.id;
    if (!projectId) return;

    this.toastService.info('Tải lên', `Đang tải lên tệp "${file.name}"...`);

    this.projectService.uploadSpecFile(projectId, file, this.uploadTargetParentId).subscribe({
      next: res => {
        this.toastService.success('Tải lên', `Tệp "${file.name}" đã được đồng bộ vào Spec Hub.`);
        this.loadTree(res.id);
        if (this.uploadTargetParentId) {
          this.expandedFolderIds.update(s => new Set(s).add(this.uploadTargetParentId!));
        }
      },
      error: () => {
        this.toastService.error('Tải lên', 'Lỗi khi tải tệp lên Spec Hub.');
      }
    });
  }

  // --- MODAL: ĐỔI TÊN ---
  openRenameModal(node: SpecTreeNode, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.renameItemId.set(node.id);
    this.renameItemName.set(node.name);
    this.isRenameModalOpen.set(true);
  }

  submitRename(): void {
    const id = this.renameItemId();
    const newName = this.renameItemName().trim();
    if (!id || !newName) return;

    this.projectService.renameSpecItem(id, newName).subscribe({
      next: () => {
        this.isRenameModalOpen.set(false);
        this.toastService.success('Đổi tên', 'Đã đổi tên thành công.');
        this.openTabs.update(tabs =>
          tabs.map(t => (t.id === id ? { ...t, name: newName } : t))
        );
        this.loadTree(id);
      },
      error: () => {
        this.toastService.error('Đổi tên', 'Không thể đổi tên đối tượng.');
      }
    });
  }

  // --- XÓA TỆP / THƯ MỤC ---
  deleteItem(node: SpecTreeNode, event?: MouseEvent): void {
    if (event) event.stopPropagation();

    const isDir = node.isDirectory;
    const msg = isDir
      ? `Bạn có chắc chắn muốn xóa thư mục "${node.name}" và toàn bộ tệp bên trong? Thao tác này sẽ xóa đồng thời trên đĩa cứng.`
      : `Bạn có chắc chắn muốn xóa tệp "${node.name}" trên hệ thống và đĩa cứng?`;

    this.confirmService.confirm({
      title: isDir ? 'Xóa thư mục Spec Hub' : 'Xóa tệp tin Spec Hub',
      message: msg,
      confirmText: 'Xóa vĩnh viễn',
      cancelText: 'Hủy',
      type: 'danger',
      onConfirm: () => {
        this.projectService.deleteSpecItem(node.id).subscribe({
          next: () => {
            this.toastService.success('Spec Hub', `Đã xóa "${node.name}".`);
            this.closeTab(node.id);
            this.loadTree();
          },
          error: () => {
            this.toastService.error('Spec Hub', 'Lỗi khi xóa đối tượng.');
          }
        });
      }
    });
  }

  // --- BÓC TÁCH USER STORY SANG BACKLOG ---
  onTextSelection(): void {
    const selection = window.getSelection();
    if (selection && selection.toString().trim().length > 0) {
      this.selectedHighlightText.set(selection.toString().trim());
    }
  }

  openExtractStoryModal(): void {
    const selectionText = this.selectedHighlightText() || window.getSelection()?.toString().trim() || '';
    this.extractExcerpt.set(selectionText);

    const firstSentence = selectionText.split('\n')[0].replace(/^[#-*\s]+/, '').trim();
    this.extractStoryTitle.set(
      firstSentence
        ? firstSentence.length > 80
          ? firstSentence.slice(0, 77) + '...'
          : firstSentence
        : 'Người dùng muốn...'
    );
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
    const tab = this.currentTab();
    const title = this.extractStoryTitle().trim();
    if (!tab || !title) {
      this.toastService.warning('Bóc tách Story', 'Vui lòng nhập tiêu đề cho User Story.');
      return;
    }

    const ac = this.extractCriteria().map(c => c.trim()).filter(c => c.length > 0);
    this.projectService
      .extractStoryFromDoc(tab.id, title, this.extractExcerpt(), this.extractStoryPoints(), ac)
      .subscribe({
        next: res => {
          this.isExtractModalOpen.set(false);
          this.toastService.success('Bóc tách Story', `Đã tạo thành công Story ${res.issueKey}: ${res.title}`);
          this.projectService.loadWorkspace();
        },
        error: () => {
          this.toastService.error('Bóc tách Story', 'Không thể bóc tách Story từ tài liệu.');
        }
      });
  }

  // --- BÌNH LUẬN & PHẢN BIỆN ---
  submitComment(): void {
    const tab = this.currentTab();
    const text = this.newCommentText().trim();
    if (!tab || !text) return;

    this.projectService.addDocumentComment(tab.id, text, this.selectedHighlightText() || undefined).subscribe({
      next: () => {
        this.toastService.success('Phản biện', 'Đã gửi ý kiến phản biện.');
        this.newCommentText.set('');
        this.selectedHighlightText.set('');
      },
      error: () => {
        this.toastService.error('Phản biện', 'Không thể gửi phản biện.');
      }
    });
  }

  getFileIcon(extension?: string | null): string {
    const ext = (extension ?? '').toLowerCase();
    switch (ext) {
      case '.pdf':
        return 'picture_as_pdf';
      case '.docx':
        return 'article';
      case '.xlsx':
        return 'table_chart';
      case '.png':
      case '.jpg':
      case '.jpeg':
      case '.gif':
      case '.webp':
      case '.svg':
      case '.bmp':
        return 'image';
      case '.md':
        return 'markdown';
      case '.txt':
      default:
        return 'description';
    }
  }

  getFileIconColor(extension?: string | null): string {
    const ext = (extension ?? '').toLowerCase();
    switch (ext) {
      case '.pdf':
        return 'text-rose-500';
      case '.docx':
        return 'text-blue-500';
      case '.xlsx':
        return 'text-emerald-500';
      case '.png':
      case '.jpg':
      case '.jpeg':
      case '.gif':
      case '.webp':
      case '.svg':
      case '.bmp':
        return 'text-purple-500';
      case '.md':
        return 'text-cyan-500';
      case '.txt':
      default:
        return 'text-amber-500';
    }
  }

  formatFileSize(bytes?: number): string {
    if (!bytes || bytes <= 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }

  // --- KÉO THẢ DI CHUYỂN TÀI LIỆU TRONG CÂY THƯ MỤC ---
  onNodeDragStart(node: SpecTreeNode, event: DragEvent): void {
    if (!this.isManager()) return;
    this.draggedNode.set(node);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', node.id);
    }
  }

  onNodeDragEnd(): void {
    this.draggedNode.set(null);
    this.dragOverFolderId.set(null);
  }

  onFolderDragOver(targetFolder: SpecTreeNode | null, event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
    this.dragOverFolderId.set(targetFolder ? targetFolder.id : 'root');
  }

  onFolderDragLeave(targetFolder: SpecTreeNode | null, event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const folderId = targetFolder ? targetFolder.id : 'root';
    if (this.dragOverFolderId() === folderId) {
      this.dragOverFolderId.set(null);
    }
  }

  onFolderDrop(targetFolder: SpecTreeNode | null, event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOverFolderId.set(null);

    // 1. Kiểm tra nếu là file/thư mục kéo thả từ máy tính Desktop vào
    if (event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files.length > 0) {
      void this.handleDesktopFilesDrop(event.dataTransfer, targetFolder ? targetFolder.id : null);
      return;
    }

    // 2. Kéo thả nội bộ di chuyển giữa các thư mục trong cây tài liệu
    const dragged = this.draggedNode();
    if (!dragged) return;

    const targetId = targetFolder ? targetFolder.id : null;
    if (dragged.id === targetId || dragged.parentId === targetId) return;

    this.projectService.moveDocument(dragged.id, targetId).subscribe({
      next: () => {
        this.toastService.success('Di chuyển tài liệu', `Đã di chuyển "${dragged.name}" thành công.`);
        this.loadTree(dragged.id);
        if (targetId) {
          this.expandedFolderIds.update(set => new Set(set).add(targetId));
        }
      },
      error: () => {
        this.toastService.error('Di chuyển tài liệu', `Không thể di chuyển "${dragged.name}".`);
      }
    });
    this.draggedNode.set(null);
  }

  // --- KÉO THẢ CẢ THƯ MỤC VÀ NHIỀU FILE TỪ MÁY TÍNH VÀO SPEC HUB ---
  private async handleDesktopFilesDrop(dataTransfer: DataTransfer, parentId: string | null): Promise<void> {
    const projectId = this.activeProject()?.id;
    if (!projectId) return;

    const items = dataTransfer.items;
    const fileEntries: { file: File; relativePath?: string }[] = [];

    const traverseEntry = async (entry: any, path: string): Promise<void> => {
      if (entry.isFile) {
        const file: File = await new Promise((resolve, reject) => entry.file(resolve, reject));
        fileEntries.push({ file, relativePath: path ? `${path}/${file.name}` : file.name });
      } else if (entry.isDirectory) {
        const dirReader = entry.createReader();
        const readEntries = async (): Promise<any[]> => {
          return new Promise((resolve, reject) => {
            dirReader.readEntries(resolve, reject);
          });
        };
        let entries: any[] = [];
        let batch: any[];
        do {
          batch = await readEntries();
          entries = entries.concat(batch);
        } while (batch && batch.length > 0);

        for (const child of entries) {
          await traverseEntry(child, path ? `${path}/${entry.name}` : entry.name);
        }
      }
    };

    if (items && items.length > 0 && typeof items[0].webkitGetAsEntry === 'function') {
      for (let i = 0; i < items.length; i++) {
        const entry = items[i].webkitGetAsEntry();
        if (entry) {
          await traverseEntry(entry, '');
        }
      }
    } else {
      for (let i = 0; i < dataTransfer.files.length; i++) {
        const f = dataTransfer.files[i];
        fileEntries.push({ file: f, relativePath: f.name });
      }
    }

    if (fileEntries.length === 0) return;

    this.toastService.info('Spec Hub', `Đang tải lên ${fileEntries.length} tệp tin và cấu trúc thư mục...`);

    this.projectService.uploadMultipleSpecFiles(projectId, fileEntries, parentId).subscribe({
      next: () => {
        this.toastService.success('Spec Hub', `Đã đồng bộ thành công ${fileEntries.length} tệp vào Spec Hub.`);
        this.loadTree();
        if (parentId) {
          this.expandedFolderIds.update(s => new Set(s).add(parentId));
        }
      },
      error: () => {
        this.toastService.error('Spec Hub', 'Lỗi khi tải hàng loạt tệp tin lên Spec Hub.');
      }
    });
  }

  // --- MARKDOWN VIEWER & GITHUB-STYLE PREVIEW ---
  toggleMarkdownMode(tab: OpenSpecTab, mode: 'view' | 'edit'): void {
    this.openTabs.update(tabs =>
      tabs.map(t => (t.id === tab.id ? { ...t, markdownMode: mode } : t))
    );
  }

  renderMarkdown(md?: string): SafeHtml {
    if (!md) return this.sanitizer.bypassSecurityTrustHtml('<p class="italic text-slate-400">Tài liệu markdown trống.</p>');

    let html = md
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');

    // Fenced Code Blocks
    html = html.replace(/```([a-zA-Z0-9_-]*)\n([\s\S]*?)```/g, (_, lang, code) => {
      return `<pre class="my-3 p-3.5 rounded-xl bg-slate-900 text-slate-100 font-mono text-xs overflow-x-auto border border-slate-800"><div class="text-[10px] text-slate-400 mb-1.5 font-bold uppercase tracking-wider">${lang || 'code'}</div><code>${code.trim()}</code></pre>`;
    });

    // Inline Code
    html = html.replace(/`([^`]+)`/g, '<code class="px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-primary font-mono text-xs font-semibold">$1</code>');

    // Headings
    html = html.replace(/^#### (.*$)/gim, '<h4 class="text-sm font-bold text-slate-900 dark:text-white mt-4 mb-2">$1</h4>');
    html = html.replace(/^### (.*$)/gim, '<h3 class="text-base font-bold text-slate-900 dark:text-white mt-5 mb-2 pb-1 border-b border-slate-100 dark:border-slate-800">$1</h3>');
    html = html.replace(/^## (.*$)/gim, '<h2 class="text-lg font-bold text-slate-900 dark:text-white mt-6 mb-2 pb-1 border-b border-slate-200 dark:border-slate-700">$1</h2>');
    html = html.replace(/^# (.*$)/gim, '<h1 class="text-xl font-bold text-slate-900 dark:text-white mt-6 mb-3 pb-2 border-b border-slate-200 dark:border-slate-700">$1</h1>');

    // Blockquotes
    html = html.replace(/^\> (.*$)/gim, '<blockquote class="border-l-4 border-primary pl-4 py-1.5 my-2.5 italic text-slate-600 dark:text-slate-300 bg-slate-50 dark:bg-slate-850/60 rounded-r-lg">$1</blockquote>');

    // Bold, Italic, Strikethrough
    html = html.replace(/\*\*([^*]+)\*\*/g, '<strong class="font-bold text-slate-900 dark:text-white">$1</strong>');
    html = html.replace(/\*([^*]+)\*/g, '<em class="italic">$1</em>');
    html = html.replace(/~~([^~]+)~~/g, '<del class="line-through text-slate-400">$1</del>');

    // Task list items
    html = html.replace(/^- \[x\] (.*$)/gim, '<li class="flex items-center gap-2 list-none my-1"><span class="material-symbols-outlined text-emerald-500 text-[18px]">check_box</span><span class="line-through text-slate-400">$1</span></li>');
    html = html.replace(/^- \[ \] (.*$)/gim, '<li class="flex items-center gap-2 list-none my-1"><span class="material-symbols-outlined text-slate-400 text-[18px]">check_box_outline_blank</span><span>$1</span></li>');

    // Unordered lists
    html = html.replace(/^[*-] (.*$)/gim, '<li class="ml-4 list-disc my-0.5">$1</li>');

    // Tables
    const lines = html.split('\n');
    let inTable = false;
    let tableHtml = '';
    const processedLines: string[] = [];

    for (const line of lines) {
      if (line.trim().startsWith('|') && line.trim().endsWith('|')) {
        const cells = line.trim().slice(1, -1).split('|').map(c => c.trim());
        if (cells.every(c => /^[-:]+$/.test(c))) {
          continue;
        }
        if (!inTable) {
          inTable = true;
          tableHtml = '<div class="overflow-x-auto my-3"><table class="min-w-full divide-y divide-slate-200 dark:divide-slate-700 border border-slate-200 dark:border-slate-700 text-xs text-left">';
          tableHtml += '<thead><tr class="bg-slate-50 dark:bg-slate-800">' + cells.map(c => `<th class="px-3 py-2 font-bold">${c}</th>`).join('') + '</tr></thead><tbody class="divide-y divide-slate-100 dark:divide-slate-800">';
        } else {
          tableHtml += '<tr>' + cells.map(c => `<td class="px-3 py-2">${c}</td>`).join('') + '</tr>';
        }
      } else {
        if (inTable) {
          inTable = false;
          tableHtml += '</tbody></table></div>';
          processedLines.push(tableHtml);
          tableHtml = '';
        }
        processedLines.push(line);
      }
    }
    if (inTable) {
      tableHtml += '</tbody></table></div>';
      processedLines.push(tableHtml);
    }

    html = processedLines.join('\n');
    html = html.replace(/\n\n/g, '<div class="h-2"></div>');

    return this.sanitizer.bypassSecurityTrustHtml(html);
  }
}
