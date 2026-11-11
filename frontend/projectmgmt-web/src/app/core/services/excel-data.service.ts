import { Injectable, inject } from '@angular/core';
import { ProjectManagementService, Sprint, WorkItem } from './project-management.service';

export interface ImportedTaskRow {
  title: string;
  description?: string;
  type?: string;
  priority?: string;
  storyPoints?: number;
  sprintName?: string;
  assigneeName?: string;
}

interface OrderedTaskEntry {
  item: WorkItem;
  isSubtask: boolean;
  parentKey: string;
}

@Injectable({ providedIn: 'root' })
export class ExcelDataService {
  private readonly projectService = inject(ProjectManagementService, { optional: true });

  /**
   * Xuất dữ liệu công việc & toàn bộ các Sprint ra file Excel (.xlsx) chuẩn Jira
   * Đảm bảo rõ từng Cell, từng Column, từng Row, kẻ khung, định dạng màu sắc và độ rộng cột.
   */
  exportToExcel(
    items: WorkItem[],
    filename: string = 'Jira_Sprint_Backlog_Export.xlsx',
    sprints?: Sprint[],
    projectName?: string
  ): void {
    const allSprints: Sprint[] =
      sprints && sprints.length > 0
        ? sprints
        : (this.projectService?.sprints() ?? []);

    const activeProject = this.projectService?.currentProject();
    const resolvedProjectName =
      projectName ||
      activeProject?.name ||
      activeProject?.projectKey ||
      'Dự án Scrum';

    const xlsxFilename = filename.replace(/\.csv$/i, '.xlsx').endsWith('.xlsx')
      ? filename.replace(/\.csv$/i, '.xlsx')
      : `${filename}.xlsx`;

    // Lọc bỏ Epic khỏi danh sách công việc dòng (vì Epic đã hiển thị ở cột Epic của từng công việc)
    const taskItems = items.filter(i => i.issueType !== 'Epic');

    const sheet1Xml = this.buildSprintGroupedWorksheetXml(taskItems, allSprints, resolvedProjectName);
    const sheet2Xml = this.buildFlatJiraWorksheetXml(taskItems, allSprints);
    const stylesXml = this.buildStylesXml();
    const workbookXml = this.buildWorkbookXml();
    const workbookRelsXml = this.buildWorkbookRelsXml();
    const rootRelsXml = this.buildRootRelsXml();
    const contentTypesXml = this.buildContentTypesXml();

    const zipBytes = this.createZipArchive([
      { path: '[Content_Types].xml', content: contentTypesXml },
      { path: '_rels/.rels', content: rootRelsXml },
      { path: 'xl/workbook.xml', content: workbookXml },
      { path: 'xl/_rels/workbook.xml.rels', content: workbookRelsXml },
      { path: 'xl/styles.xml', content: stylesXml },
      { path: 'xl/worksheets/sheet1.xml', content: sheet1Xml },
      { path: 'xl/worksheets/sheet2.xml', content: sheet2Xml }
    ]);

    const blob = new Blob([zipBytes.buffer as ArrayBuffer], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    });
    const url = URL.createObjectURL(blob);

    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', xlsxFilename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  private organizeHierarchy(sectionItems: WorkItem[], allItems: WorkItem[]): OrderedTaskEntry[] {
    const parentItems = sectionItems.filter(
      i => !i.parentId && i.issueType !== 'Sub-task' && i.issueType !== 'Subtask'
    );
    const subtaskMap = new Map<string, WorkItem[]>();
    for (const item of allItems) {
      if (item.parentId) {
        const list = subtaskMap.get(item.parentId) || [];
        list.push(item);
        subtaskMap.set(item.parentId, list);
      }
    }

    const ordered: OrderedTaskEntry[] = [];
    const processedIds = new Set<string>();

    for (const parent of parentItems) {
      ordered.push({ item: parent, isSubtask: false, parentKey: '' });
      processedIds.add(parent.id);

      const subs = subtaskMap.get(parent.id) || [];
      for (const sub of subs) {
        ordered.push({
          item: sub,
          isSubtask: true,
          parentKey: parent.issueKey || parent.id
        });
        processedIds.add(sub.id);
      }
    }

    for (const item of sectionItems) {
      if (!processedIds.has(item.id)) {
        const parent = allItems.find(p => p.id === item.parentId);
        ordered.push({
          item,
          isSubtask: Boolean(
            item.parentId || item.issueType === 'Sub-task' || item.issueType === 'Subtask'
          ),
          parentKey: parent ? parent.issueKey || parent.id : item.parentId || ''
        });
        processedIds.add(item.id);
      }
    }

    return ordered;
  }

  private getPriorityLabel(priority?: string): string {
    switch (priority) {
      case 'Urgent':
        return 'Khẩn cấp (Urgent)';
      case 'High':
        return 'Cao (High)';
      case 'Medium':
        return 'Trung bình (Medium)';
      case 'Low':
        return 'Thấp (Low)';
      default:
        return priority || 'Trung bình (Medium)';
    }
  }

  private getPriorityStyleIndex(priority?: string): number {
    if (priority === 'Urgent' || priority === 'High') return 9;
    if (priority === 'Medium') return 10;
    return 11;
  }

  private getStatusStyleIndex(statusName?: string): number {
    const s = (statusName || '').toLowerCase();
    if (s.includes('done') || s.includes('hoàn thành') || s.includes('closed')) return 12;
    if (s.includes('progress') || s.includes('review') || s.includes('qa') || s.includes('testing')) return 13;
    return 14;
  }

  private getSprintStatusLabel(status?: string): string {
    if (status === 'active') return 'Đang chạy';
    if (status === 'completed') return 'Hoàn thành';
    return 'Kế hoạch';
  }

  /**
   * Sheet 1: Báo cáo phân nhóm theo toàn bộ các Sprint hiện có & Backlog chuẩn Jira
   * Các cột: A: Mã | B: Công việc | C: Loại | D: Epic | E: Sprint | F: Ưu tiên | G: SP | H: Trạng thái | I: Người xử lý | J: Mô tả
   */
  private buildSprintGroupedWorksheetXml(
    items: WorkItem[],
    sprints: Sprint[],
    projectName: string
  ): string {
    const rowsXml: string[] = [];
    const mergeRefs: string[] = [];
    let rowIdx = 1;

    const nowStr = new Date().toLocaleDateString('vi-VN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });

    // Dòng 1: Banner tiêu đề dự án
    const titleText = `BÁO CÁO SPRINT & BACKLOG — Dự án: ${projectName}   |   Xuất lúc: ${nowStr}   |   Tổng số Sprint: ${sprints.length}   |   Tổng công việc: ${items.length}`;
    rowsXml.push(this.buildMergedBannerRow(rowIdx, titleText, 1, 32));
    mergeRefs.push(`A${rowIdx}:J${rowIdx}`);
    rowIdx++;

    // Dòng 2: Dòng trống ngăn cách
    rowsXml.push(`<row r="${rowIdx}" ht="10" customHeight="1"/>`);
    rowIdx++;

    const sprintIds = new Set(sprints.map(s => s.id));

    // Lặp qua TOÀN BỘ các Sprint đang có trong dự án
    for (const sp of sprints) {
      const rawSprintTasks = items.filter(i => i.sprintId === sp.id);
      const orderedSprintTasks = this.organizeHierarchy(rawSprintTasks, items);
      const totalSp = orderedSprintTasks.reduce((sum, entry) => sum + (entry.item.storyPoints || 0), 0);
      const dateRange =
        sp.startDate || sp.endDate
          ? `${sp.startDate || '...'} – ${sp.endDate || '...'}`
          : 'Chưa thiết lập ngày';
      const goalText = sp.goal || 'Hoàn thành hạng mục cam kết';
      const statusText = this.getSprintStatusLabel(sp.status);

      // Banner tên Sprint
      const sprintBanner = `SPRINT: ${sp.name} (${dateRange})   •   Trạng thái: ${statusText}   •   Mục tiêu: ${goalText}   •   Số lượng: ${orderedSprintTasks.length} công việc (${totalSp} SP)`;
      rowsXml.push(this.buildMergedBannerRow(rowIdx, sprintBanner, 2, 26));
      mergeRefs.push(`A${rowIdx}:J${rowIdx}`);
      rowIdx++;

      // Header cột của bảng Sprint
      rowsXml.push(this.buildTableHeaderRow(rowIdx));
      rowIdx++;

      if (orderedSprintTasks.length > 0) {
        for (const entry of orderedSprintTasks) {
          rowsXml.push(this.buildTaskDataRow(rowIdx, entry, sp.name));
          rowIdx++;
        }
      } else {
        rowsXml.push(
          this.buildMergedBannerRow(
            rowIdx,
            `(Chưa có công việc nào trong ${sp.name})`,
            15,
            22
          )
        );
        mergeRefs.push(`A${rowIdx}:J${rowIdx}`);
        rowIdx++;
      }

      // Dòng trống giữa các Sprint
      rowsXml.push(`<row r="${rowIdx}" ht="12" customHeight="1"/>`);
      rowIdx++;
    }

    // Khối Backlog (Các công việc chưa gán vào Sprint nào)
    const rawBacklogTasks = items.filter(
      i => !i.sprintId || i.sprintId === 'sp-backlog' || !sprintIds.has(i.sprintId)
    );
    const orderedBacklogTasks = this.organizeHierarchy(rawBacklogTasks, items);
    const backlogSp = orderedBacklogTasks.reduce((sum, entry) => sum + (entry.item.storyPoints || 0), 0);

    const backlogBanner = `BACKLOG (Danh sách công việc chờ lập kế hoạch)   •   Số lượng: ${orderedBacklogTasks.length} công việc (${backlogSp} SP)`;
    rowsXml.push(this.buildMergedBannerRow(rowIdx, backlogBanner, 2, 26));
    mergeRefs.push(`A${rowIdx}:J${rowIdx}`);
    rowIdx++;

    rowsXml.push(this.buildTableHeaderRow(rowIdx));
    rowIdx++;

    if (orderedBacklogTasks.length > 0) {
      for (const entry of orderedBacklogTasks) {
        rowsXml.push(this.buildTaskDataRow(rowIdx, entry, 'Backlog'));
        rowIdx++;
      }
    } else {
      rowsXml.push(
        this.buildMergedBannerRow(rowIdx, '(Backlog hiện đang trống)', 15, 22)
      );
      mergeRefs.push(`A${rowIdx}:J${rowIdx}`);
      rowIdx++;
    }

    const mergeCellsXml =
      mergeRefs.length > 0
        ? `<mergeCells count="${mergeRefs.length}">${mergeRefs
            .map(ref => `<mergeCell ref="${ref}"/>`)
            .join('')}</mergeCells>`
        : '';

    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <sheetViews>
    <sheetView workbookViewId="0" showGridLines="1"/>
  </sheetViews>
  <sheetFormatPr defaultRowHeight="22"/>
  ${this.buildColumnsXml()}
  <sheetData>
    ${rowsXml.join('\n    ')}
  </sheetData>
  ${mergeCellsXml}
</worksheet>`;
  }

  /**
   * Sheet 2: Bảng tổng hợp phẳng chuẩn Jira Issue Navigator (có AutoFilter & Freeze Header)
   */
  private buildFlatJiraWorksheetXml(items: WorkItem[], sprints: Sprint[]): string {
    const sprintNameMap = new Map<string, string>();
    for (const sp of sprints) {
      sprintNameMap.set(sp.id, sp.name);
    }

    const rowsXml: string[] = [];
    let rowIdx = 1;

    // Dòng 1: Tiêu đề cột
    rowsXml.push(this.buildTableHeaderRow(rowIdx));
    rowIdx++;

    // Sắp xếp theo từng Sprint trước, sau đó đến Backlog
    for (const sp of sprints) {
      const spItems = items.filter(i => i.sprintId === sp.id);
      const ordered = this.organizeHierarchy(spItems, items);
      for (const entry of ordered) {
        rowsXml.push(this.buildTaskDataRow(rowIdx, entry, sp.name));
        rowIdx++;
      }
    }

    const sprintIds = new Set(sprints.map(s => s.id));
    const backlogItems = items.filter(
      i => !i.sprintId || i.sprintId === 'sp-backlog' || !sprintIds.has(i.sprintId)
    );
    const orderedBacklog = this.organizeHierarchy(backlogItems, items);
    for (const entry of orderedBacklog) {
      const spLabel =
        (entry.item.sprintId && sprintNameMap.get(entry.item.sprintId)) ||
        entry.item.sprintName ||
        'Backlog';
      rowsXml.push(this.buildTaskDataRow(rowIdx, entry, spLabel));
      rowIdx++;
    }

    const lastRow = Math.max(1, rowIdx - 1);

    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <sheetViews>
    <sheetView workbookViewId="0" showGridLines="1">
      <pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>
    </sheetView>
  </sheetViews>
  <sheetFormatPr defaultRowHeight="22"/>
  ${this.buildColumnsXml()}
  <sheetData>
    ${rowsXml.join('\n    ')}
  </sheetData>
  <autoFilter ref="A1:J${lastRow}"/>
</worksheet>`;
  }

  private buildColumnsXml(): string {
    return `<cols>
    <col min="1" max="1" width="15" customWidth="1"/>
    <col min="2" max="2" width="40" customWidth="1"/>
    <col min="3" max="3" width="14" customWidth="1"/>
    <col min="4" max="4" width="25" customWidth="1"/>
    <col min="5" max="5" width="22" customWidth="1"/>
    <col min="6" max="6" width="20" customWidth="1"/>
    <col min="7" max="7" width="10" customWidth="1"/>
    <col min="8" max="8" width="18" customWidth="1"/>
    <col min="9" max="9" width="24" customWidth="1"/>
    <col min="10" max="10" width="52" customWidth="1"/>
  </cols>`;
  }

  private buildTableHeaderRow(rowIdx: number): string {
    const headers = [
      'Mã',
      'Công việc',
      'Loại',
      'Epic',
      'Sprint',
      'Ưu tiên',
      'SP',
      'Trạng thái',
      'Người xử lý',
      'Mô tả'
    ];
    const cols = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J'];
    const cells = headers
      .map((h, i) => this.buildStringCell(`${cols[i]}${rowIdx}`, h, 3))
      .join('');
    return `<row r="${rowIdx}" ht="25" customHeight="1">${cells}</row>`;
  }

  private buildMergedBannerRow(
    rowIdx: number,
    text: string,
    styleIdx: number,
    height: number
  ): string {
    const cols = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J'];
    const cells = cols
      .map((col, idx) =>
        idx === 0
          ? this.buildStringCell(`${col}${rowIdx}`, text, styleIdx)
          : `<c r="${col}${rowIdx}" s="${styleIdx}"/>`
      )
      .join('');
    return `<row r="${rowIdx}" ht="${height}" customHeight="1">${cells}</row>`;
  }

  private buildTaskDataRow(
    rowIdx: number,
    entry: OrderedTaskEntry,
    sprintName: string
  ): string {
    const { item, isSubtask, parentKey } = entry;
    const issueKey = item.issueKey || item.id;
    const titleDisplay = isSubtask
      ? `   ↳ [${parentKey}] ${item.title || ''}`
      : item.title || '';
    const issueType = item.issueType || (isSubtask ? 'Sub-task' : 'Task');
    const epicDisplay = item.epicName?.trim() ? item.epicName : 'Chưa gán Epic';
    const sprintDisplay =
      sprintName === 'Backlog Pool' ? 'Backlog' : sprintName || 'Backlog';
    const priorityDisplay = this.getPriorityLabel(item.priority);
    const spValue =
      item.storyPoints !== undefined && item.storyPoints !== null
        ? Number(item.storyPoints) || 0
        : 0;
    const statusDisplay = item.statusName || 'To Do';
    const assigneeDisplay = item.assigneeName?.trim()
      ? item.assigneeName
      : 'Chưa xử lý';
    const descriptionDisplay = (item.description || '').trim();

    const lineCount = Math.max(
      1,
      descriptionDisplay.split(/\r?\n/).length,
      Math.ceil(descriptionDisplay.length / 48),
      Math.ceil(titleDisplay.length / 36)
    );
    const rowHeight = Math.min(120, Math.max(24, lineCount * 18));

    const cells = [
      this.buildStringCell(`A${rowIdx}`, issueKey, 4),
      this.buildStringCell(`B${rowIdx}`, titleDisplay, isSubtask ? 16 : 5),
      this.buildStringCell(`C${rowIdx}`, issueType, 7),
      this.buildStringCell(`D${rowIdx}`, epicDisplay, item.epicName ? 8 : 7),
      this.buildStringCell(`E${rowIdx}`, sprintDisplay, 6),
      this.buildStringCell(
        `F${rowIdx}`,
        priorityDisplay,
        this.getPriorityStyleIndex(item.priority)
      ),
      this.buildNumberCell(`G${rowIdx}`, spValue, 7),
      this.buildStringCell(
        `H${rowIdx}`,
        statusDisplay,
        this.getStatusStyleIndex(statusDisplay)
      ),
      this.buildStringCell(`I${rowIdx}`, assigneeDisplay, 6),
      this.buildStringCell(`J${rowIdx}`, descriptionDisplay, 6)
    ].join('');

    return `<row r="${rowIdx}" ht="${rowHeight}" customHeight="1">${cells}</row>`;
  }

  private buildStringCell(ref: string, value: string, styleIdx: number): string {
    return `<c r="${ref}" s="${styleIdx}" t="inlineStr"><is><t xml:space="preserve">${this.escapeXml(
      value
    )}</t></is></c>`;
  }

  private buildNumberCell(ref: string, value: number, styleIdx: number): string {
    return `<c r="${ref}" s="${styleIdx}"><v>${value}</v></c>`;
  }

  private escapeXml(str: string): string {
    return String(str ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;');
  }

  private buildStylesXml(): string {
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="10">
    <font><sz val="10.5"/><color rgb="FF0F172A"/><name val="Segoe UI"/></font>
    <font><b/><sz val="10.5"/><color rgb="FFFFFFFF"/><name val="Segoe UI"/></font>
    <font><b/><sz val="12.5"/><color rgb="FFFFFFFF"/><name val="Segoe UI"/></font>
    <font><b/><sz val="10.5"/><color rgb="FF1E3A8A"/><name val="Segoe UI"/></font>
    <font><b/><sz val="10.5"/><color rgb="FF0052CC"/><name val="Segoe UI"/></font>
    <font><b/><sz val="10"/><color rgb="FFB91C1C"/><name val="Segoe UI"/></font>
    <font><b/><sz val="10"/><color rgb="FF15803D"/><name val="Segoe UI"/></font>
    <font><b/><sz val="10"/><color rgb="FF1D4ED8"/><name val="Segoe UI"/></font>
    <font><i/><sz val="10"/><color rgb="FF64748B"/><name val="Segoe UI"/></font>
    <font><b/><sz val="10.5"/><color rgb="FF0F172A"/><name val="Segoe UI"/></font>
  </fonts>
  <fills count="11">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FF0F172A"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FF0052CC"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFDBEAFE"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFF8FAFC"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFDCFCE7"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFE0F2FE"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFFEE2E2"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFFEF3C7"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFF3E8FF"/><bgColor indexed="64"/></patternFill></fill>
  </fills>
  <borders count="3">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border>
      <left style="thin"><color rgb="FFCBD5E1"/></left>
      <right style="thin"><color rgb="FFCBD5E1"/></right>
      <top style="thin"><color rgb="FFCBD5E1"/></top>
      <bottom style="thin"><color rgb="FFCBD5E1"/></bottom>
      <diagonal/>
    </border>
    <border>
      <left style="medium"><color rgb="FF3B82F6"/></left>
      <right style="medium"><color rgb="FF3B82F6"/></right>
      <top style="medium"><color rgb="FF3B82F6"/></top>
      <bottom style="medium"><color rgb="FF3B82F6"/></bottom>
      <diagonal/>
    </border>
  </borders>
  <cellStyleXfs count="1">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
  </cellStyleXfs>
  <cellXfs count="17">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
    <xf numFmtId="0" fontId="2" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" indent="1"/></xf>
    <xf numFmtId="0" fontId="3" fillId="4" borderId="2" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="1" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="4" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="9" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="0" fillId="10" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="5" fillId="8" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="0" fillId="9" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="0" fillId="5" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="6" fillId="6" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="7" fillId="7" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="0" fillId="5" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="8" fillId="5" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="0" fillId="5" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
  </cellXfs>
</styleSheet>`;
  }

  private buildWorkbookXml(): string {
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="Theo Sprint &amp; Backlog" sheetId="1" r:id="rId1"/>
    <sheet name="Danh sách Tổng hợp (Jira)" sheetId="2" r:id="rId2"/>
  </sheets>
</workbook>`;
  }

  private buildWorkbookRelsXml(): string {
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>`;
  }

  private buildRootRelsXml(): string {
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>`;
  }

  private buildContentTypesXml(): string {
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
</Types>`;
  }

  /**
   * Đóng gói các file XML thành định dạng nén chuẩn ZIP (.xlsx)
   */
  private createZipArchive(entries: { path: string; content: string }[]): Uint8Array {
    const encoder = new TextEncoder();
    const localParts: Uint8Array[] = [];
    const centralParts: Uint8Array[] = [];
    let offset = 0;

    for (const entry of entries) {
      const nameBytes = encoder.encode(entry.path);
      const dataBytes = encoder.encode(entry.content);
      const crc = this.crc32(dataBytes);

      const localHeader = new Uint8Array(30 + nameBytes.length);
      const localView = new DataView(localHeader.buffer);
      localView.setUint32(0, 0x04034b50, true); // Local file header signature
      localView.setUint16(4, 20, true); // Version needed to extract (2.0)
      localView.setUint16(6, 0x0800, true); // UTF-8 flag
      localView.setUint16(8, 0, true); // Compression method: 0 (STORE)
      localView.setUint16(10, 0, true); // File last mod time
      localView.setUint16(12, 0x21, true); // File last mod date
      localView.setUint32(14, crc, true); // CRC-32
      localView.setUint32(18, dataBytes.length, true); // Compressed size
      localView.setUint32(22, dataBytes.length, true); // Uncompressed size
      localView.setUint16(26, nameBytes.length, true); // File name length
      localView.setUint16(28, 0, true); // Extra field length
      localHeader.set(nameBytes, 30);

      localParts.push(localHeader, dataBytes);

      const centralHeader = new Uint8Array(46 + nameBytes.length);
      const centralView = new DataView(centralHeader.buffer);
      centralView.setUint32(0, 0x02014b50, true); // Central directory signature
      centralView.setUint16(4, 20, true); // Version made by
      centralView.setUint16(6, 20, true); // Version needed
      centralView.setUint16(8, 0x0800, true); // UTF-8 flag
      centralView.setUint16(10, 0, true); // Compression: STORE
      centralView.setUint16(12, 0, true);
      centralView.setUint16(14, 0x21, true);
      centralView.setUint32(16, crc, true);
      centralView.setUint32(20, dataBytes.length, true);
      centralView.setUint32(24, dataBytes.length, true);
      centralView.setUint16(28, nameBytes.length, true);
      centralView.setUint16(30, 0, true);
      centralView.setUint16(32, 0, true);
      centralView.setUint16(34, 0, true);
      centralView.setUint16(36, 0, true);
      centralView.setUint32(38, 0, true);
      centralView.setUint32(42, offset, true); // Offset of local header
      centralHeader.set(nameBytes, 46);

      centralParts.push(centralHeader);
      offset += localHeader.length + dataBytes.length;
    }

    const centralSize = centralParts.reduce((sum, p) => sum + p.length, 0);
    const eocd = new Uint8Array(22);
    const eocdView = new DataView(eocd.buffer);
    eocdView.setUint32(0, 0x06054b50, true); // End of central directory signature
    eocdView.setUint16(4, 0, true);
    eocdView.setUint16(6, 0, true);
    eocdView.setUint16(8, entries.length, true);
    eocdView.setUint16(10, entries.length, true);
    eocdView.setUint32(12, centralSize, true);
    eocdView.setUint32(16, offset, true);
    eocdView.setUint16(20, 0, true);

    const totalLength = offset + centralSize + eocd.length;
    const output = new Uint8Array(totalLength);
    let pos = 0;
    for (const part of localParts) {
      output.set(part, pos);
      pos += part.length;
    }
    for (const part of centralParts) {
      output.set(part, pos);
      pos += part.length;
    }
    output.set(eocd, pos);
    return output;
  }

  private crc32(data: Uint8Array): number {
    let crc = 0xffffffff;
    for (let i = 0; i < data.length; i++) {
      crc ^= data[i];
      for (let j = 0; j < 8; j++) {
        crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0);
      }
    }
    return (crc ^ 0xffffffff) >>> 0;
  }

  /**
   * Download a standard Excel/CSV template for importing tasks
   */
  downloadTemplate(): void {
    const headers = [
      'Tên công việc (Bắt buộc)',
      'Mô tả',
      'Loại (Task/Bug/Story/Epic)',
      'Độ ưu tiên (Low/Medium/High/Urgent)',
      'Story Points',
      'Tên Sprint',
      'Người thực hiện'
    ];
    const sampleRows = [
      [
        'Thiết kế DB Schema cho Auth Module',
        'Tạo các bảng User, Role, Permission',
        'Task',
        'High',
        5,
        'Sprint 1',
        'Nguyễn Văn A'
      ],
      [
        'Kiểm thử giao diện người dùng',
        'Kiểm tra độ tương thích trên các kích thước màn hình',
        'Bug',
        'Urgent',
        2,
        'Sprint 1',
        'Trần Thị B'
      ],
      [
        'Tích hợp API bóc tách công việc',
        'Kết nối dịch vụ xử lý công việc tự động',
        'Story',
        'Medium',
        8,
        'Sprint 2',
        'Lê Văn C'
      ]
    ];

    const csvContent =
      '\uFEFF' +
      [headers.join(','), ...sampleRows.map(r => r.map(c => `"${c}"`).join(','))].join('\r\n');
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
