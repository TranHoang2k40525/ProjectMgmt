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
   * Export Work Items list to formatted CSV / Excel file with full details and sub-task hierarchy
   */
  exportToExcel(items: WorkItem[], filename: string = 'Scrum_Backlog_Export.csv', sprints: any[] = []): void {
    const headers = [
      'Mã công việc',
      'Loại công việc',
      'Phân cấp',
      'Công việc cha',
      'Tên công việc',
      'Mô tả chi tiết',
      'Epic',
      'Chu kỳ Sprint',
      'Trạng thái Sprint',
      'Trạng thái',
      'Mức độ ưu tiên',
      'Story Points',
      'Giờ ước tính (h)',
      'Giờ còn lại (h)',
      'Tình trạng cản trở',
      'Lý do cản trở',
      'Người thực hiện',
      'Người tạo',
      'Ngày tạo',
      'Ngày cập nhật'
    ];

    const sprintMap = new Map<string, { name: string; status: string }>();
    for (const sp of sprints) {
      const statusLabel = sp.status === 'active' ? 'Đang chạy' : (sp.status === 'completed' ? 'Hoàn thành' : 'Kế hoạch');
      sprintMap.set(sp.id, { name: sp.name, status: statusLabel });
    }

    const priorityLabel = (p: string) => {
      switch (p) {
        case 'Urgent': return 'Khẩn cấp';
        case 'High': return 'Cao';
        case 'Medium': return 'Trung bình';
        case 'Low': return 'Thấp';
        default: return p || 'Trung bình';
      }
    };

    // Build parent-child mapping for sub-tasks
    const parentItems = items.filter(i => !i.parentId && i.issueType !== 'Sub-task' && i.issueType !== 'Subtask');
    const subtaskMap = new Map<string, WorkItem[]>();
    for (const item of items) {
      if (item.parentId) {
        const list = subtaskMap.get(item.parentId) || [];
        list.push(item);
        subtaskMap.set(item.parentId, list);
      }
    }

    // Organize items: Parent followed by its sub-tasks
    const orderedItems: { item: WorkItem; isSubtask: boolean; parentKey: string }[] = [];
    const processedIds = new Set<string>();

    for (const parent of parentItems) {
      orderedItems.push({ item: parent, isSubtask: false, parentKey: '' });
      processedIds.add(parent.id);

      const subtasks = subtaskMap.get(parent.id) || [];
      for (const sub of subtasks) {
        orderedItems.push({ item: sub, isSubtask: true, parentKey: parent.issueKey || parent.id });
        processedIds.add(sub.id);
      }
    }

    // Append any orphaned subtasks or items without matching parent
    for (const item of items) {
      if (!processedIds.has(item.id)) {
        const parent = items.find(p => p.id === item.parentId);
        orderedItems.push({
          item,
          isSubtask: Boolean(item.parentId || item.issueType === 'Sub-task' || item.issueType === 'Subtask'),
          parentKey: parent ? (parent.issueKey || parent.id) : (item.parentId || '')
        });
      }
    }

    const escapeCsv = (val: unknown): string => {
      if (val === null || val === undefined) return '""';
      const str = String(val).replace(/"/g, '""');
      return `"${str}"`;
    };

    const formatDate = (dateStr?: string | null): string => {
      if (!dateStr) return '';
      try {
        const d = new Date(dateStr);
        return isNaN(d.getTime()) ? dateStr : d.toLocaleDateString('vi-VN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
      } catch {
        return dateStr || '';
      }
    };

    const rows = orderedItems.map(({ item, isSubtask, parentKey }) => {
      const spInfo = item.sprintId ? sprintMap.get(item.sprintId) : null;
      const sprintName = spInfo?.name || item.sprintName || 'Backlog';
      const sprintStatus = spInfo?.status || (item.sprintId ? 'Trong Sprint' : 'Chưa gán (Backlog)');
      const blockerStatus = item.isBlocked ? 'Bị cản trở' : 'Thông suốt';

      return [
        escapeCsv(item.issueKey || item.id),
        escapeCsv(item.issueType || (isSubtask ? 'Sub-task' : 'Story')),
        escapeCsv(isSubtask ? 'Công việc con (Sub-task)' : 'Công việc chính'),
        escapeCsv(parentKey),
        escapeCsv(item.title || ''),
        escapeCsv(item.description || ''),
        escapeCsv(item.epicName || ''),
        escapeCsv(sprintName),
        escapeCsv(sprintStatus),
        escapeCsv(item.statusName || 'To Do'),
        escapeCsv(priorityLabel(item.priority)),
        item.storyPoints !== undefined && item.storyPoints !== null ? item.storyPoints : 0,
        item.estimatedHours !== undefined && item.estimatedHours !== null ? item.estimatedHours : 0,
        item.remainingHours !== undefined && item.remainingHours !== null ? item.remainingHours : 0,
        escapeCsv(blockerStatus),
        escapeCsv(item.blockReason || ''),
        escapeCsv(item.assigneeName || 'Chưa chỉ định'),
        escapeCsv(item.reporterName || 'Admin'),
        escapeCsv(formatDate(item.createdAt)),
        escapeCsv(formatDate(item.updatedAt))
      ].join(',');
    });

    const csvContent = '\uFEFF' + [headers.map(escapeCsv).join(','), ...rows].join('\r\n');
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
      ['Thiết kế DB Schema cho Auth Module', 'Tạo các bảng User, Role, Permission', 'Task', 'High', 5, 'Sprint 1', 'Nguyễn Văn A'],
      ['Kiểm thử giao diện người dùng', 'Kiểm tra độ tương thích trên các kích thước màn hình', 'Bug', 'Urgent', 2, 'Sprint 1', 'Trần Thị B'],
      ['Tích hợp API bóc tách công việc', 'Kết nối dịch vụ xử lý công việc tự động', 'Story', 'Medium', 8, 'Sprint 2', 'Lê Văn C']
    ];

    const csvContent = '\uFEFF' + [headers.join(','), ...sampleRows.map(r => r.map(c => `"${c}"`).join(','))].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', 'Task_Import_Template.csv');
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
