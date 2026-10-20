import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable } from 'rxjs';
import { ProjectManagementService, WorkItem } from '../../core/services/project-management.service';

@Component({ selector: 'app-issue-detail-page', imports: [CommonModule, FormsModule, RouterLink], templateUrl: './issue-detail-page.html', styleUrl: './issue-detail-page.scss' })
export class IssueDetailPage implements OnInit {
  readonly service = inject(ProjectManagementService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  readonly error = signal(''); readonly message = signal(''); readonly loading = signal(false); readonly busy = signal(false);
  readonly priorities = ['Low', 'Medium', 'High', 'Urgent'];
  item: WorkItem | null = null; newComment = ''; newCriterion = ''; subtaskTitle = '';
  ngOnInit(): void { this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => this.load(params.get('id') ?? '')); }
  load(id = this.item?.id ?? ''): void {
    this.loading.set(true);
    this.service.getIssue(id).subscribe({ next: task => {
      this.item = { ...task }; this.loading.set(false);
      if (this.service.currentProject().id !== task.projectId) this.service.getProjectById(task.projectId).subscribe({ next: p => this.service.currentProject.set(p), error: e => this.fail(e) });
    }, error: e => { this.item = null; this.loading.set(false); this.fail(e); } });
  }
  private fail(error: { detail?: string; title?: string }): void { this.busy.set(false); this.error.set(error.detail ?? error.title ?? 'Không thể thực hiện thao tác.'); }
  private run(request: Observable<unknown>, message: string, clear?: () => void): void {
    if (this.busy()) return; this.busy.set(true); this.error.set('');
    request.subscribe({ next: () => { clear?.(); this.busy.set(false); this.message.set(message); this.load(); this.service.loadWorkspace(); }, error: e => this.fail(e) });
  }
  save(): void { if (this.item) this.run(this.service.saveIssue(this.item), 'Đã lưu công việc.'); }
  comment(): void { if (this.item) this.run(this.service.postComment(this.item.id, this.newComment), 'Đã lưu bình luận.', () => this.newComment = ''); }
  addCriterion(): void { if (this.item) this.run(this.service.addCriterion(this.item.id, this.newCriterion), 'Đã thêm tiêu chí.', () => this.newCriterion = ''); }
  toggleCriterion(c: { id: string; content: string; isMet: boolean }): void { this.run(this.service.updateCriterion(c.id, c.content, !c.isMet), 'Đã lưu tiêu chí.'); }
  upload(event: Event): void { const input = event.target as HTMLInputElement; const file = input.files?.[0]; if (file && this.item) this.run(this.service.uploadAttachment(this.item.id, file), 'Đã tải tệp lên.', () => input.value = ''); }
  download(id: string, name: string): void { this.service.downloadAttachment(id).subscribe({ next: blob => { const url = URL.createObjectURL(blob); const a = document.createElement('a'); a.href = url; a.download = name; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); }, error: e => this.fail(e) }); }
  removeAttachment(id: string): void { this.run(this.service.deleteAttachment(id), 'Đã xóa tệp.'); }
  addSubtask(): void { if (this.item) this.run(this.service.createIssue(this.item.projectId, { title: this.subtaskTitle, issueType: 'Sub-task', parentId: this.item.id, statusName: this.service.projectStatuses().find(s => s.isInitial)?.name ?? 'To Do', priority: 'Medium', storyPoints: 0 }), 'Đã tạo công việc con.', () => this.subtaskTitle = ''); }
}
