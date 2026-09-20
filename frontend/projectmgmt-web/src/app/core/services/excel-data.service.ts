import { Injectable } from '@angular/core';
import { WorkItem } from './project-management.service';

export interface ImportedTaskRow {
  title: string;
  description?: string;
  type?: string;
  priority?: string;
  storyPoints?: number;
  sprintName?: string;
  assigneeName?: string;
}

@Injectable({ providedIn: 'root' })
export class ExcelDataService {

  /**
   * Export Work Items list to formatted CSV / Excel file
   */
  exportToExcel(items: WorkItem[], filename: string = 'HUCE_Scrum_WorkItems.csv'): void {
    const headers = ['Mã Task', 'Tiêu đề', 'Loại', 'Trạng thái', 'Mức độ ưu tiên', 'Story Points', 'Sprint', 'Người thực hiện', 'Người tạo', 'Ngày cập nhật'];
    
    const rows = items.map(item => [
      `"${item.issueKey || item.id}"`,
      `"${(item.title || '').replace(/"/g, '""')}"`,
      `"${item.issueType || 'Task'}"`,
      `"${item.statusName || 'To Do'}"`,
      `"${item.priority || 'Medium'}"`,
      item.storyPoints || 0,
      `"${item.sprintName || 'Backlog'}"`,
      `"${item.assigneeName || 'Unassigned'}"`,
      `"${item.reporterName || 'Admin'}"`,
      `"${item.updatedAt ? new Date(item.updatedAt).toLocaleDateString('vi-VN') : ''}"`
    ]);

    const csvContent = '\uFEFF' + [headers.join(','), ...rows.map(r => r.join(','))].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  /**
   * Download a standard Excel/CSV template for importing tasks
   */
  downloadTemplate(): void {
    const headers = ['Tên công việc (Bắt buộc)', 'Mô tả', 'Loại (Task/Bug/Story/Epic)', 'Độ ưu tiên (Low/Medium/High/Urgent)', 'Story Points', 'Tên Sprint', 'Người thực hiện'];
    const sampleRows = [
      ['Thiết kế DB Schema cho Auth Module', 'Tạo các bảng User, Role, Permission', 'Task', 'High', 5, 'Sprint 1', 'Trần Văn Hoàng'],
      ['Lỗi không load được ảnh avatar', 'Kiểm tra đường dẫn assets trong angular.json', 'Bug', 'Urgent', 2, 'Sprint 2', 'Nguyễn Văn A'],
      ['Tích hợp Claude AI Auto Breakdown', 'Tự động bóc tách Epic thành subtask', 'Story', 'Medium', 8, 'Sprint 2', 'Trần Văn Hoàng']
    ];

    const csvContent = '\uFEFF' + [headers.join(','), ...sampleRows.map(r => r.map(c => `"${c}"`).join(','))].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', 'HUCE_Task_Import_Template.csv');
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  /**
   * Parse CSV / Excel text content into task rows
   */
  parseCsv(content: string): ImportedTaskRow[] {
    const lines = content.split(/\r?\n/).filter(line => line.trim().length > 0);
    if (lines.length <= 1) return [];

    const results: ImportedTaskRow[] = [];
    
    for (let i = 1; i < lines.length; i++) {
      const line = lines[i];
      // Regex to parse CSV with quoted commas
      const matches = line.match(/(".*?"|[^",\s]+)(?=\s*,|\s*$)/g) || line.split(',');
      if (!matches || matches.length === 0) continue;

      const clean = matches.map(m => m.replace(/^"|"$/g, '').trim());
      const title = clean[0];
      if (!title) continue;

      results.push({
        title,
        description: clean[1] || '',
        type: clean[2] || 'Task',
        priority: clean[3] || 'Medium',
        storyPoints: parseInt(clean[4], 10) || 1,
        sprintName: clean[5] || 'Sprint 1',
        assigneeName: clean[6] || 'Unassigned'
      });
    }

    return results;
  }
}
