import { Component, EventEmitter, Output, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-create-project-modal',
  template: `<div role="dialog" aria-label="Tạo dự án">
    <p>Tạo dự án với tổ chức và người phụ trách.</p>
    <button type="button" (click)="openForm()">Mở form tạo dự án</button>
    <button type="button" (click)="dismissed.emit()">Hủy</button>
  </div>`
})
export class CreateProjectModalComponent {
  @Output() dismissed = new EventEmitter<void>();
  private readonly router = inject(Router);
  openForm(): void {
    this.dismissed.emit();
    void this.router.navigate(['/projects/new']);
  }
}
