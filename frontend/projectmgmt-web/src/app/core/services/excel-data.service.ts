import { Injectable, inject } from '@angular/core';
import { ProjectManagementService, ProjectStatus, Sprint, WorkItem } from './project-management.service';
import type { AiExcelExportSpecDto, AiSprintProposalDto, ProposedTaskDto } from './ai-agent.service';

export interface ImportedTaskRow {
  title: string;
  description?: string;
  type?: string;
  priority?: string;
  storyPoints?: number;
  sprintName?: string;
  assigneeName?: string;
}

export type ExcelExportScope =
  | 'project_all'
  | 'single_sprint'
  | 'sprint_list'
  | 'backlog_only'
  | 'single_task'
  | 'filtered_tasks'
  | 'task_detail'
  | 'ai_proposal';

export interface ExcelExportOptions {
  scope?: ExcelExportScope;
  title?: string;
  subtitle?: string;
  filename?: string;
  sprintIds?: string[];
  sprintNames?: string[];
  issueIds?: string[];
  issueKeys?: string[];
  assigneeNames?: string[];
  statusNames?: string[];
  priorities?: string[];
  issueTypes?: string[];
  epicNames?: string[];
  searchKeyword?: string;
  blockedOnly?: boolean | null;
  includeSubtasks?: boolean;
  includeLegend?: boolean;
  projectName?: string;
  projectKey?: string;
  projectStatuses?: ProjectStatus[];
  proposal?: AiSprintProposalDto | null;
  /** Nếu truyền true ở task_list / filtered, ẩn các Sprint trống và chỉ giữ một bảng dữ liệu sạch */
  flatOnly?: boolean;
}

interface OrderedTaskEntry {
  item: WorkItem;
  isSubtask: boolean;
  parentKey: string;
}

interface ResolvedStatusStyle {
  name: string;
  description: string;
  bgHex: string;
  textHex: string;
  styleIndex: number;
  category: string;
}

interface StyleRegistry {
  stylesXml: string;
  statusStyleMap: Map<string, ResolvedStatusStyle>;
  legendEntries: ResolvedStatusStyle[];
  blockedStyleIndex: number;
}

interface BuiltSheetSpec {
  name: string;
  xml: string;
}

export interface ExcelWorkbookArtifacts {
  filename: string;
  itemCount: number;
  sheets: BuiltSheetSpec[];
  stylesXml: string;
  workbookXml: string;
}

@Injectable({ providedIn: 'root' })
export class ExcelDataService {
  private readonly projectService = inject(ProjectManagementService, { optional: true });

  private static readonly BASE_BLOCKED_STYLE_INDEX = 15;
  private static readonly FIRST_STATUS_STYLE_INDEX = 16;

  /**
   * Xuất dữ liệu công việc ProjectMgmt ra tệp Excel (.xlsx) theo định dạng bảng sạch,
   * chỉ tô màu ô Trạng thái (và cờ Bị chặn nếu có) kèm bảng Chú thích tương ứng.
   */
  exportToExcel(
    items: WorkItem[],
    filename: string = 'ProjectMgmt_Bao_Cao_Cong_Viec.xlsx',
    sprints: Sprint[] = [],
    projectName: string = 'ProjectMgmt Enterprise',
    extraOptions?: Partial<ExcelExportOptions>
  ): void {
    const options: ExcelExportOptions = {
      scope: extraOptions?.scope ?? 'project_all',
      filename: extraOptions?.filename || filename,
      projectName: extraOptions?.projectName || projectName,
      ...extraOptions,
    };
    this.exportWithOptions(items, sprints, options);
  }

  /**
   * Xuất tệp Excel theo đúng AiExcelExportSpecDto nhận về từ Trợ Lý AI.
   */
  exportFromAiSpec(
    items: WorkItem[],
    allSprints: Sprint[],
    spec: AiExcelExportSpecDto | null | undefined,
    context: {
      projectName?: string;
      projectKey?: string;
      projectStatuses?: ProjectStatus[];
      proposal?: AiSprintProposalDto | null;
    } = {}
  ): { itemCount: number; filename: string } {
    const scope = (spec?.scope as ExcelExportScope) || 'project_all';
    const options: ExcelExportOptions = {
      scope,
      title: spec?.title,
      subtitle: spec?.subtitle || undefined,
      filename: spec?.suggestedFileName || undefined,
      sprintIds: spec?.sprintIds ?? [],
      sprintNames: spec?.sprintNames ?? [],
      issueIds: spec?.issueIds ?? [],
      issueKeys: spec?.issueKeys ?? [],
      assigneeNames: spec?.assigneeNames ?? [],
      statusNames: spec?.statusNames ?? [],
      priorities: spec?.priorities ?? [],
      issueTypes: spec?.issueTypes ?? [],
      epicNames: spec?.epicNames ?? [],
      searchKeyword: spec?.searchKeyword || undefined,
      blockedOnly: spec?.blockedOnly ?? null,
      includeSubtasks: spec?.includeSubtasks ?? true,
      includeLegend: spec?.includeLegend ?? true,
      projectName: context.projectName,
      projectKey: context.projectKey,
      projectStatuses: context.projectStatuses,
      proposal: context.proposal ?? null,
    };

    return this.exportWithOptions(items, allSprints, options);
  }

  /**
   * Xuất tệp Excel chi tiết đầy đủ cho một công việc (dùng trực tiếp từ Ngăn kéo Chi tiết).
   */
  exportSingleTaskDetail(
    task: WorkItem,
    allItems: WorkItem[],
    projectName: string = 'ProjectMgmt Enterprise',
    projectStatuses?: ProjectStatus[]
  ): { itemCount: number; filename: string } {
    const today = new Date().toISOString().slice(0, 10).replace(/-/g, '');
    const safeKey = (task.issueKey || 'TASK').replace(/[^\w\-]/g, '');
    return this.exportWithOptions(allItems, this.projectService?.sprints() ?? [], {
      scope: 'task_detail',
      title: `HỒ SƠ CHI TIẾT CÔNG VIỆC ${task.issueKey || ''}`.trim(),
      subtitle: `Dự án: ${projectName} | Trạng thái: ${task.statusName} | Ưu tiên: ${task.priority}`,
      filename: `${safeKey}_Chi_Tiet_${today}.xlsx`,
      issueIds: task.id ? [task.id] : [],
      issueKeys: task.issueKey ? [task.issueKey] : [],
      includeSubtasks: true,
      includeLegend: true,
      projectName,
      projectStatuses,
    });
  }

  exportWithOptions(
    items: WorkItem[],
    allSprints: Sprint[],
    options: ExcelExportOptions = {}
  ): { itemCount: number; filename: string } {
    const artifacts = this.buildWorkbookArtifacts(items, allSprints, options);
    this.triggerXlsxDownload(artifacts);
    return { itemCount: artifacts.itemCount, filename: artifacts.filename };
  }

  /**
   * Xây dựng toàn bộ cấu trúc OpenXML của Workbook .xlsx (tách thuần túy để dễ kiểm thử đơn vị).
   */
  buildWorkbookArtifacts(
    items: WorkItem[],
    allSprints: Sprint[],
    options: ExcelExportOptions = {}
  ): ExcelWorkbookArtifacts {
    const scope: ExcelExportScope = options.scope ?? 'project_all';
    const projectName = options.projectName || this.projectService?.currentProject()?.name || 'ProjectMgmt Enterprise';
    const projectKey = options.projectKey || this.projectService?.currentProject()?.projectKey || 'PM';
    const statuses =
      options.projectStatuses && options.projectStatuses.length > 0
        ? options.projectStatuses
        : (this.projectService?.projectStatuses() ?? []);
    const allItems = items || [];
    const sprints = allSprints || [];
    const includeSubtasks = options.includeSubtasks ?? true;
    const includeLegend = options.includeLegend ?? true;
    const dateStamp = new Date().toISOString().slice(0, 10).replace(/-/g, '');
    const defaultFileName = `${this.sanitizeFilenameToken(projectKey)}_Bao_Cao_${dateStamp}.xlsx`;
    const filename = this.normalizeXlsxFilename(options.filename || defaultFileName);

    // Nếu xuất đề xuất AI (hoặc chi tiết 1 task trong đề xuất AI)
    if (
      scope === 'ai_proposal' ||
      (scope === 'task_detail' && options.searchKeyword === 'ai_proposal' && options.proposal?.tasks?.length)
    ) {
      const proposal = options.proposal;
      const allProposalTasks = proposal?.tasks ?? [];
      const targetIds = new Set((options.issueKeys ?? []).map((x) => x.toLowerCase()));
      const proposalTasks =
        scope === 'ai_proposal' && targetIds.size > 0
          ? allProposalTasks.filter((t) => targetIds.has((t.id || '').toLowerCase()))
          : allProposalTasks;
      const collectedStatuses = this.collectStatusNamesFromProposal(proposalTasks);
      const registry = this.buildStyleRegistry(statuses, collectedStatuses);

      if (scope === 'task_detail' && allProposalTasks.length > 0) {
        const targetId = (options.issueKeys?.[0] || '').toLowerCase();
        const foundTask =
          allProposalTasks.find((t) => (t.id || '').toLowerCase() === targetId) ?? allProposalTasks[0];
        const syntheticItem = this.mapProposedTaskToWorkItem(foundTask, proposal);
        const syntheticSubs = (foundTask.subtasks ?? []).map((st, idx) =>
          this.mapProposedSubtaskToWorkItem(st, foundTask, idx)
        );
        const xml = this.buildTaskDetailWorksheetXml(
          syntheticItem,
          syntheticSubs,
          projectName,
          options.title || `CHI TIẾT ĐỀ XUẤT - ${foundTask.title}`,
          registry,
          includeLegend,
          foundTask
        );
        const sheets: BuiltSheetSpec[] = [
          { name: this.sanitizeSheetName(`Chi tiết ${foundTask.id || 'Đề xuất'}`), xml },
        ];
        const workbookXml = this.buildWorkbookXml(sheets);
        return {
          filename,
          itemCount: 1,
          sheets,
          stylesXml: registry.stylesXml,
          workbookXml,
        };
      }

      const xml = this.buildProposalWorksheetXml(
        proposalTasks,
        proposal,
        projectName,
        options.title || `BẢNG ĐỀ XUẤT KẾ HOẠCH AI - ${projectName}`,
        options.subtitle || '',
        registry,
        includeSubtasks,
        includeLegend
      );
      const sheets: BuiltSheetSpec[] = [{ name: this.sanitizeSheetName('Đề xuất AI'), xml }];
      const workbookXml = this.buildWorkbookXml(sheets);
      const totalCount =
        proposalTasks.length +
        (includeSubtasks ? proposalTasks.reduce((s, t) => s + (t.subtasks?.length || 0), 0) : 0);
      return {
        filename,
        itemCount: totalCount,
        sheets,
        stylesXml: registry.stylesXml,
        workbookXml,
      };
    }

    // Lọc dữ liệu công việc thực tế theo ExportOptions
    const filtered = this.filterWorkItems(allItems, sprints, options);
    const registry = this.buildStyleRegistry(statuses, filtered.map((i) => ({
      name: i.statusName,
      color: i.statusColor,
    })));

    if (scope === 'task_detail') {
      const target = this.pickPrimaryDetailTarget(filtered, allItems, options);
      if (!target) {
        const emptySheet = this.buildTableWorksheetXml(
          [],
          sprints,
          projectName,
          options.title || 'CHI TIẾT CÔNG VIỆC',
          'Không tìm thấy công việc phù hợp.',
          registry,
          includeLegend,
          false
        );
        const sheets: BuiltSheetSpec[] = [{ name: 'Chi tiết Công việc', xml: emptySheet }];
        return {
          filename,
          itemCount: 0,
          sheets,
          stylesXml: registry.stylesXml,
          workbookXml: this.buildWorkbookXml(sheets),
        };
      }

      const subtasks = allItems.filter((i) => this.eqId(i.parentId, target.id));
      const detailXml = this.buildTaskDetailWorksheetXml(
        target,
        subtasks,
        projectName,
        options.title || `HỒ SƠ CHI TIẾT CÔNG VIỆC ${target.issueKey || ''}`.trim(),
        registry,
        includeLegend
      );
      const sheetName = this.sanitizeSheetName(`Chi tiết ${target.issueKey || 'Công việc'}`);
      const sheets: BuiltSheetSpec[] = [{ name: sheetName, xml: detailXml }];
      return {
        filename,
        itemCount: 1,
        sheets,
        stylesXml: registry.stylesXml,
        workbookXml: this.buildWorkbookXml(sheets),
      };
    }

    const ordered = this.orderWorkItemsHierarchically(filtered, includeSubtasks);
    const defaultTitle = this.resolveDefaultTitle(scope, projectName, sprints, options);
    const subtitle = options.subtitle || this.buildDefaultSubtitle(scope, projectName, sprints, options, ordered.length);

    if (scope === 'project_all' && !options.flatOnly) {
      const activeSprintList = this.selectRelevantSprints(sprints, options);
      const groupedXml = this.buildGroupedWorksheetXml(
        ordered.map((e) => e.item),
        activeSprintList,
        projectName,
        options.title || defaultTitle,
        subtitle,
        registry,
        includeLegend,
        true
      );
      const flatXml = this.buildTableWorksheetXml(
        ordered,
        sprints,
        projectName,
        `DANH SÁCH CÔNG VIỆC TỔNG HỢP - ${projectName.toUpperCase()}`,
        subtitle,
        registry,
        includeLegend,
        true
      );
      const sheets: BuiltSheetSpec[] = [
        { name: 'Theo Sprint & Backlog', xml: groupedXml },
        { name: 'Danh sách Tổng hợp', xml: flatXml },
      ];
      return {
        filename,
        itemCount: ordered.length,
        sheets,
        stylesXml: registry.stylesXml,
        workbookXml: this.buildWorkbookXml(sheets),
      };
    }

    if (scope === 'sprint_list' && !options.flatOnly) {
      const targetSprints = this.selectRelevantSprints(sprints, options);
      const flatXml = this.buildTableWorksheetXml(
        ordered,
        targetSprints,
        projectName,
        options.title || defaultTitle,
        subtitle,
        registry,
        includeLegend,
        true
      );
      const groupedXml = this.buildGroupedWorksheetXml(
        ordered.map((e) => e.item),
        targetSprints,
        projectName,
        options.title || defaultTitle,
        subtitle,
        registry,
        includeLegend,
        false
      );
      const sheets: BuiltSheetSpec[] = [
        { name: 'Danh sách Công việc', xml: flatXml },
        { name: 'Theo từng Sprint', xml: groupedXml },
      ];
      return {
        filename,
        itemCount: ordered.length,
        sheets,
        stylesXml: registry.stylesXml,
        workbookXml: this.buildWorkbookXml(sheets),
      };
    }

    // Các phạm vi gọn: single_sprint, backlog_only, single_task, filtered_tasks -> 1 sheet bảng sạch không thừa cột/sprint rỗng
    const singleSheetName = this.resolveFocusedSheetName(scope, sprints, options);
    const sheetXml = this.buildTableWorksheetXml(
      ordered,
      sprints,
      projectName,
      options.title || defaultTitle,
      subtitle,
      registry,
      includeLegend,
      scope !== 'backlog_only'
    );
    const sheets: BuiltSheetSpec[] = [{ name: singleSheetName, xml: sheetXml }];
    return {
      filename,
      itemCount: ordered.length,
      sheets,
      stylesXml: registry.stylesXml,
      workbookXml: this.buildWorkbookXml(sheets),
    };
  }

  private triggerXlsxDownload(artifacts: ExcelWorkbookArtifacts): void {
    const contentTypesXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
${artifacts.sheets
  .map(
    (_, idx) =>
      `  <Override PartName="/xl/worksheets/sheet${idx + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>`
  )
  .join('\n')}
</Types>`;

    const rootRelsXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>`;

    const sheetRels = artifacts.sheets
      .map(
        (_, idx) =>
          `  <Relationship Id="rId${idx + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet${idx + 1}.xml"/>`
      )
      .join('\n');
    const styleRelId = `rId${artifacts.sheets.length + 1}`;
    const workbookRelsXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
${sheetRels}
  <Relationship Id="${styleRelId}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>`;

    const entries: Array<{ name: string; data: Uint8Array }> = [
      { name: '[Content_Types].xml', data: this.utf8Bytes(contentTypesXml) },
      { name: '_rels/.rels', data: this.utf8Bytes(rootRelsXml) },
      { name: 'xl/_rels/workbook.xml.rels', data: this.utf8Bytes(workbookRelsXml) },
      { name: 'xl/workbook.xml', data: this.utf8Bytes(artifacts.workbookXml) },
      { name: 'xl/styles.xml', data: this.utf8Bytes(artifacts.stylesXml) },
      ...artifacts.sheets.map((s, idx) => ({
        name: `xl/worksheets/sheet${idx + 1}.xml`,
        data: this.utf8Bytes(s.xml),
      })),
    ];

    const zipBytes = this.createZipArchive(entries);
    const blob = new Blob([zipBytes as unknown as BlobPart], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });

    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = artifacts.filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  // =========================================================================
  // BỘ LỌC DỮ LIỆU CHÍNH XÁC THEO YÊU CẦU
  // =========================================================================

  private filterWorkItems(
    allItems: WorkItem[],
    sprints: Sprint[],
    options: ExcelExportOptions
  ): WorkItem[] {
    const scope = options.scope ?? 'project_all';
    const includeSubtasks = options.includeSubtasks ?? true;
    const explicitIssueTypes = (options.issueTypes ?? []).map((t) => t.toLowerCase());
    const includesEpicExplicitly =
      explicitIssueTypes.includes('epic') || scope === 'task_detail' || scope === 'single_task';

    const issueIdsSet = new Set((options.issueIds ?? []).map((x) => x.toLowerCase()));
    const issueKeysSet = new Set((options.issueKeys ?? []).map((x) => x.toUpperCase()));
    const hasExplicitIssueTarget = issueIdsSet.size > 0 || issueKeysSet.size > 0;

    if (hasExplicitIssueTarget) {
      const matchedParents = allItems.filter(
        (i) =>
          (i.id && issueIdsSet.has(i.id.toLowerCase())) ||
          (i.issueKey && issueKeysSet.has(i.issueKey.toUpperCase()))
      );
      if (!includeSubtasks || scope === 'task_detail') {
        return matchedParents;
      }
      const matchedParentIdSet = new Set(matchedParents.map((p) => p.id.toLowerCase()));
      const children = allItems.filter(
        (i) => i.parentId && matchedParentIdSet.has(i.parentId.toLowerCase())
      );
      const seen = new Set<string>();
      return [...matchedParents, ...children].filter((item) => {
        const k = item.id.toLowerCase();
        if (seen.has(k)) return false;
        seen.add(k);
        return true;
      });
    }

    let pool = allItems.filter(
      (i) => includesEpicExplicitly || (i.issueType || '').toLowerCase() !== 'epic'
    );

    if (scope === 'single_sprint' || scope === 'sprint_list') {
      const targetIds = new Set((options.sprintIds ?? []).map((x) => x.toLowerCase()));
      const targetNames = new Set((options.sprintNames ?? []).map((x) => x.toLowerCase()));

      if (targetIds.size === 0 && targetNames.size > 0) {
        for (const sp of sprints) {
          if (targetNames.has((sp.name || '').toLowerCase())) {
            targetIds.add(sp.id.toLowerCase());
          }
        }
      }

      pool = pool.filter((i) => {
        const sid = (i.sprintId || '').toLowerCase();
        const sname = (i.sprintName || '').toLowerCase();
        return (sid && targetIds.has(sid)) || (sname && targetNames.has(sname));
      });
    } else if (scope === 'backlog_only') {
      pool = pool.filter((i) => !i.sprintId || i.sprintId === 'sp-backlog');
    }

    if (!includeSubtasks) {
      pool = pool.filter(
        (i) => !i.parentId && (i.issueType || '').toLowerCase() !== 'sub-task' && (i.issueType || '').toLowerCase() !== 'subtask'
      );
    }

    const assignees = options.assigneeNames ?? [];
    const statuses = (options.statusNames ?? []).map((s) => s.toLowerCase());
    const priorities = (options.priorities ?? []).map((p) => p.toLowerCase());
    const epics = (options.epicNames ?? []).map((e) => e.toLowerCase());
    const kw = (options.searchKeyword || '').trim().toLowerCase();

    return pool.filter((item) => {
      if (assignees.length > 0) {
        const allowUnassigned = assignees.includes('__UNASSIGNED__');
        const nameLower = (item.assigneeName || '').trim().toLowerCase();
        const matchesUnassigned = allowUnassigned && !nameLower;
        const matchesNamed = assignees.some(
          (a) => a !== '__UNASSIGNED__' && nameLower === a.trim().toLowerCase()
        );
        if (!matchesUnassigned && !matchesNamed) return false;
      }

      if (statuses.length > 0 && !statuses.includes((item.statusName || '').toLowerCase())) {
        return false;
      }

      if (priorities.length > 0 && !priorities.includes((item.priority || '').toLowerCase())) {
        return false;
      }

      if (explicitIssueTypes.length > 0) {
        const tLower = (item.issueType || '').toLowerCase();
        const matchesType = explicitIssueTypes.some(
          (t) => tLower === t || (t.includes('sub') && tLower.includes('sub'))
        );
        if (!matchesType) return false;
      }

      if (epics.length > 0 && !epics.includes((item.epicName || '').toLowerCase())) {
        return false;
      }

      if (options.blockedOnly && !item.isBlocked) {
        return false;
      }

      if (kw) {
        const hay = `${item.issueKey} ${item.title} ${item.description} ${item.assigneeName} ${item.epicName || ''}`.toLowerCase();
        if (!hay.includes(kw)) return false;
      }

      return true;
    });
  }

  private pickPrimaryDetailTarget(
    filtered: WorkItem[],
    allItems: WorkItem[],
    options: ExcelExportOptions
  ): WorkItem | null {
    const idTarget = (options.issueIds?.[0] || '').toLowerCase();
    const keyTarget = (options.issueKeys?.[0] || '').toUpperCase();
    if (idTarget) {
      const found = allItems.find((i) => (i.id || '').toLowerCase() === idTarget);
      if (found) return found;
    }
    if (keyTarget) {
      const found = allItems.find((i) => (i.issueKey || '').toUpperCase() === keyTarget);
      if (found) return found;
    }
    return filtered[0] ?? null;
  }

  private selectRelevantSprints(sprints: Sprint[], options: ExcelExportOptions): Sprint[] {
    const targetIds = new Set((options.sprintIds ?? []).map((x) => x.toLowerCase()));
    if (targetIds.size > 0) {
      return sprints.filter((s) => targetIds.has(s.id.toLowerCase()));
    }
    return sprints;
  }

  private orderWorkItemsHierarchically(items: WorkItem[], includeSubtasks: boolean): OrderedTaskEntry[] {
    const itemById = new Map<string, WorkItem>();
    for (const item of items) {
      if (item.id) itemById.set(item.id.toLowerCase(), item);
    }

    const parentItems = items.filter(
      (i) => !i.parentId || !itemById.has(i.parentId.toLowerCase())
    );
    const childrenByParent = new Map<string, WorkItem[]>();
    if (includeSubtasks) {
      for (const item of items) {
        if (item.parentId && itemById.has(item.parentId.toLowerCase())) {
          const pKey = item.parentId.toLowerCase();
          const list = childrenByParent.get(pKey) ?? [];
          list.push(item);
          childrenByParent.set(pKey, list);
        }
      }
    }

    const result: OrderedTaskEntry[] = [];
    for (const parent of parentItems) {
      result.push({ item: parent, isSubtask: false, parentKey: '' });
      if (includeSubtasks && parent.id) {
        const subs = childrenByParent.get(parent.id.toLowerCase()) ?? [];
        for (const sub of subs) {
          result.push({ item: sub, isSubtask: true, parentKey: parent.issueKey || '' });
        }
      }
    }
    return result;
  }

  // =========================================================================
  // XÂY DỰNG BẢNG MÀU TRẠNG THÁI ĐỘNG & XML STYLESHEET
  // =========================================================================

  private buildStyleRegistry(
    projectStatuses: ProjectStatus[],
    observedStatuses: Array<{ name: string; color?: string }>
  ): StyleRegistry {
    const defaults: Array<{ name: string; colorHex: string; category: string; desc: string }> = [
      { name: 'To Do', colorHex: '#64748B', category: 'ToDo', desc: 'Chờ xử lý / Khởi tạo mặc định' },
      { name: 'In Progress', colorHex: '#2563EB', category: 'InProgress', desc: 'Đang triển khai thực hiện' },
      { name: 'In Review', colorHex: '#D97706', category: 'InProgress', desc: 'Chờ kiểm tra / Review' },
      { name: 'Done', colorHex: '#16A34A', category: 'Done', desc: 'Đã hoàn thành' },
    ];

    const resolvedList: Array<{ name: string; colorHex: string; category: string; desc: string }> = [];
    const seenNormalized = new Set<string>();

    if (projectStatuses && projectStatuses.length > 0) {
      const sorted = [...projectStatuses].sort((a, b) => (a.orderIndex ?? 0) - (b.orderIndex ?? 0));
      for (const st of sorted) {
        const norm = (st.name || '').trim().toLowerCase();
        if (!norm || seenNormalized.has(norm)) continue;
        seenNormalized.add(norm);
        const desc = st.isDone || st.category === 'Done'
          ? 'Đã hoàn thành'
          : st.isCanceled || st.category === 'Canceled'
            ? 'Đã hủy / Tạm dừng'
            : st.isInitial || st.category === 'ToDo'
              ? 'Chờ xử lý (Khởi tạo)'
              : 'Đang thực hiện / Kiểm tra';
        resolvedList.push({
          name: st.name.trim(),
          colorHex: this.normalizeHexColor(st.colorHex, st.category),
          category: st.category || 'ToDo',
          desc,
        });
      }
    } else {
      for (const d of defaults) {
        seenNormalized.add(d.name.toLowerCase());
        resolvedList.push(d);
      }
    }

    for (const obs of observedStatuses) {
      const rawName = (obs.name || '').trim();
      const norm = rawName.toLowerCase();
      if (!norm || seenNormalized.has(norm)) continue;
      seenNormalized.add(norm);
      const guessedCat = this.inferCategoryFromStatusName(norm);
      resolvedList.push({
        name: rawName,
        colorHex: this.normalizeHexColor(obs.color, guessedCat),
        category: guessedCat,
        desc:
          guessedCat === 'Done'
            ? 'Đã hoàn thành'
            : guessedCat === 'Canceled'
              ? 'Đã hủy / Ngưng triển khai'
              : guessedCat === 'InProgress'
                ? 'Đang triển khai / Chờ duyệt'
                : 'Chờ xử lý',
      });
    }

    const legendEntries: ResolvedStatusStyle[] = [];
    const statusStyleMap = new Map<string, ResolvedStatusStyle>();
    const extraFills: string[] = [];
    const extraFonts: string[] = [];
    const extraCellXfs: string[] = [];

    // Static Fills indices:
    // 0: none, 1: gray125, 2: #F1F5F9 (table header & totals), 3: #F8FAFC (section/kv label), 4: #FEE2E2 (blocked)
    const firstDynamicFillId = 5;
    // Static Fonts indices:
    // 0: Calibri 10 #0F172A, 1: Calibri 13 bold #0F172A, 2: Calibri 10 #475569,
    // 3: Calibri 10 bold #0F172A, 4: Calibri 10 #334155, 5: Calibri 10 italic #64748B, 6: Calibri 10 bold #991B1B
    const firstDynamicFontId = 7;

    resolvedList.forEach((st, idx) => {
      const bgHex = this.mixWithWhite(st.colorHex, 0.86);
      const textHex = this.darkenColorForContrast(st.colorHex);
      const fillId = firstDynamicFillId + idx;
      const fontId = firstDynamicFontId + idx;
      const styleIndex = ExcelDataService.FIRST_STATUS_STYLE_INDEX + idx;

      extraFills.push(`<fill><patternFill patternType="solid"><fgColor rgb="FF${bgHex}"/></patternFill></fill>`);
      extraFonts.push(`<font><b/><sz val="10"/><color rgb="FF${textHex}"/><name val="Calibri"/></font>`);
      extraCellXfs.push(
        `<xf numFmtId="0" fontId="${fontId}" fillId="${fillId}" borderId="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>`
      );

      const entry: ResolvedStatusStyle = {
        name: st.name,
        description: st.desc,
        bgHex,
        textHex,
        styleIndex,
        category: st.category,
      };
      legendEntries.push(entry);
      statusStyleMap.set(st.name.toLowerCase(), entry);
    });

    const stylesXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="${firstDynamicFontId + extraFonts.length}">
    <font><sz val="10"/><color rgb="FF0F172A"/><name val="Calibri"/></font>
    <font><b/><sz val="13"/><color rgb="FF0F172A"/><name val="Calibri"/></font>
    <font><sz val="10"/><color rgb="FF475569"/><name val="Calibri"/></font>
    <font><b/><sz val="10"/><color rgb="FF0F172A"/><name val="Calibri"/></font>
    <font><sz val="10"/><color rgb="FF334155"/><name val="Calibri"/></font>
    <font><i/><sz val="10"/><color rgb="FF64748B"/><name val="Calibri"/></font>
    <font><b/><sz val="10"/><color rgb="FF991B1B"/><name val="Calibri"/></font>
    ${extraFonts.join('\n    ')}
  </fonts>
  <fills count="${firstDynamicFillId + extraFills.length}">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFF1F5F9"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFF8FAFC"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFFEE2E2"/></patternFill></fill>
    ${extraFills.join('\n    ')}
  </fills>
  <borders count="4">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border>
      <left style="thin"><color rgb="FFCBD5E1"/></left>
      <right style="thin"><color rgb="FFCBD5E1"/></right>
      <top style="thin"><color rgb="FFCBD5E1"/></top>
      <bottom style="thin"><color rgb="FFCBD5E1"/></bottom>
      <diagonal/>
    </border>
    <border>
      <left/><right/><top/>
      <bottom style="medium"><color rgb="FF94A3B8"/></bottom>
      <diagonal/>
    </border>
    <border>
      <left style="thin"><color rgb="FF94A3B8"/></left>
      <right style="thin"><color rgb="FF94A3B8"/></right>
      <top style="thin"><color rgb="FF94A3B8"/></top>
      <bottom style="thin"><color rgb="FF94A3B8"/></bottom>
      <diagonal/>
    </border>
  </borders>
  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
  <cellXfs count="${ExcelDataService.FIRST_STATUS_STYLE_INDEX + extraCellXfs.length}">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" applyFont="1" applyAlignment="1"><alignment vertical="center"/></xf>
    <xf numFmtId="0" fontId="1" fillId="0" borderId="2" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
    <xf numFmtId="0" fontId="2" fillId="0" borderId="0" applyFont="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="3" fillId="2" borderId="3" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="3" fillId="3" borderId="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="3" fillId="0" borderId="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
    <xf numFmtId="0" fontId="4" fillId="0" borderId="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="2" fillId="0" borderId="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    <xf numFmtId="0" fontId="3" fillId="2" borderId="3" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="center"/></xf>
    <xf numFmtId="0" fontId="3" fillId="3" borderId="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="5" fillId="0" borderId="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="3" fillId="2" borderId="3" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
    <xf numFmtId="0" fontId="6" fillId="4" borderId="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
    ${extraCellXfs.join('\n    ')}
  </cellXfs>
</styleSheet>`;

    return {
      stylesXml,
      statusStyleMap,
      legendEntries,
      blockedStyleIndex: ExcelDataService.BASE_BLOCKED_STYLE_INDEX,
    };
  }

  private resolveStatusStyleIndex(statusName: string | undefined, registry: StyleRegistry): number {
    const norm = (statusName || '').trim().toLowerCase();
    const direct = registry.statusStyleMap.get(norm);
    if (direct) return direct.styleIndex;

    const guessedCat = this.inferCategoryFromStatusName(norm);
    const byCat = registry.legendEntries.find((e) => e.category.toLowerCase() === guessedCat.toLowerCase());
    return byCat ? byCat.styleIndex : 6;
  }

  // =========================================================================
  // SHEET DẠNG BẢNG SẠCH CHUẨN PROJECTMGMT (DANH SÁCH PHẲNG / PHẠM VI LỌC)
  // =========================================================================

  private buildTableWorksheetXml(
    entries: OrderedTaskEntry[],
    sprints: Sprint[],
    projectName: string,
    title: string,
    subtitle: string,
    registry: StyleRegistry,
    includeLegend: boolean,
    includeSprintColumn: boolean
  ): string {
    const sprintNameById = new Map(sprints.map((s) => [s.id.toLowerCase(), s.name]));
    const rowsXml: string[] = [];
    const mergeRefs: string[] = [];
    let rowIndex = 1;

    const colHeaders = includeSprintColumn
      ? ['STT', 'Mã việc', 'Loại', 'Tên công việc', 'Sprint / Nguồn', 'Epic', 'Ưu tiên', 'Trạng thái', 'Story Points', 'Người thực hiện', 'Ghi chú']
      : ['STT', 'Mã việc', 'Loại', 'Tên công việc', 'Epic', 'Ưu tiên', 'Trạng thái', 'Story Points', 'Người thực hiện', 'Ghi chú'];

    const lastColLetter = this.colLetter(colHeaders.length - 1);

    // Dòng 1: Tiêu đề trang nhã trên nền trắng
    rowsXml.push(`<row r="${rowIndex}" ht="26" customHeight="1">${this.strCell(`A${rowIndex}`, title, 1)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    // Dòng 2: Thông tin phạm vi & ngày xuất
    const totalPoints = entries.reduce((sum, e) => sum + (e.item.storyPoints || 0), 0);
    const infoLine = `${subtitle || `Dự án: ${projectName}`}  |  Ngày xuất: ${new Date().toLocaleDateString('vi-VN')}  |  Số lượng: ${entries.length} mục (${totalPoints} SP)`;
    rowsXml.push(`<row r="${rowIndex}" ht="20" customHeight="1">${this.strCell(`A${rowIndex}`, infoLine, 2)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    // Dòng 3: Hàng trống phân cách gọn
    rowsXml.push(`<row r="${rowIndex}" ht="8" customHeight="1"/>`);
    rowIndex++;

    // Dòng 4: Header Bảng dữ liệu
    const headerRowIndex = rowIndex;
    rowsXml.push(
      `<row r="${rowIndex}" ht="24" customHeight="1">${colHeaders
        .map((h, i) => this.strCell(`${this.colLetter(i)}${rowIndex}`, h, 3))
        .join('')}</row>`
    );
    rowIndex++;

    let stt = 0;
    let hasAnyBlocked = false;
    let hasAnySubtask = false;

    if (entries.length === 0) {
      rowsXml.push(
        `<row r="${rowIndex}" ht="22" customHeight="1">${this.strCell(
          `A${rowIndex}`,
          'Không có công việc nào khớp với phạm vi hoặc bộ lọc đã chọn.',
          13
        )}</row>`
      );
      mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
      rowIndex++;
    } else {
      for (const entry of entries) {
        const item = entry.item;
        stt++;
        if (item.isBlocked) hasAnyBlocked = true;
        if (entry.isSubtask) hasAnySubtask = true;

        const baseStyle = entry.isSubtask ? 9 : 5;
        const centerStyle = entry.isSubtask ? 10 : 6;
        const keyStyle = entry.isSubtask ? 10 : 7;
        const statusStyle = item.isBlocked
          ? registry.blockedStyleIndex
          : this.resolveStatusStyleIndex(item.statusName, registry);
        const statusLabel = item.isBlocked ? `${item.statusName} (Bị chặn)` : item.statusName || 'To Do';
        const displayTitle = entry.isSubtask
          ? `  ↳ ${item.title}${entry.parentKey ? ` (thuộc ${entry.parentKey})` : ''}`
          : item.title || '';
        const sprintLabel =
          !item.sprintId || item.sprintId === 'sp-backlog'
            ? 'Kho Product Backlog'
            : item.sprintName || sprintNameById.get(item.sprintId.toLowerCase()) || 'Sprint';
        const noteText = item.isBlocked
          ? `Bị chặn: ${item.blockReason || 'Đang vướng mắc'}`
          : item.labels && item.labels.length > 0
            ? item.labels.join(', ')
            : '';

        const cells: string[] = [];
        let colIdx = 0;
        const pushStr = (val: string, style: number) => {
          cells.push(this.strCell(`${this.colLetter(colIdx)}${rowIndex}`, val, style));
          colIdx++;
        };
        const pushNum = (val: number, style: number) => {
          cells.push(this.numCell(`${this.colLetter(colIdx)}${rowIndex}`, val, style));
          colIdx++;
        };

        pushNum(stt, centerStyle);
        pushStr(item.issueKey || '', keyStyle);
        pushStr(entry.isSubtask ? 'Việc con' : item.issueType || 'Task', centerStyle);
        pushStr(displayTitle, baseStyle);
        if (includeSprintColumn) pushStr(sprintLabel, baseStyle);
        pushStr(item.epicName || '', baseStyle);
        pushStr(item.priority || 'Medium', centerStyle);
        pushStr(statusLabel, statusStyle);
        pushNum(item.storyPoints || 0, 8);
        pushStr(item.assigneeName || 'Chưa gán', baseStyle);
        pushStr(noteText, item.isBlocked ? registry.blockedStyleIndex : baseStyle);

        const rowHt = Math.max(22, Math.min(60, 18 + Math.ceil((displayTitle.length || 20) / 52) * 14));
        rowsXml.push(`<row r="${rowIndex}" ht="${rowHt}" customHeight="1">${cells.join('')}</row>`);
        rowIndex++;
      }

      // Dòng Tổng cộng
      const spColIdx = includeSprintColumn ? 8 : 7;
      const sumCells: string[] = [];
      for (let i = 0; i < colHeaders.length; i++) {
        const ref = `${this.colLetter(i)}${rowIndex}`;
        if (i === 0) sumCells.push(this.strCell(ref, `Tổng cộng (${entries.length} việc)`, 11));
        else if (i === spColIdx) sumCells.push(this.numCell(ref, totalPoints, 11));
        else sumCells.push(this.strCell(ref, '', 11));
      }
      rowsXml.push(`<row r="${rowIndex}" ht="22" customHeight="1">${sumCells.join('')}</row>`);
      mergeRefs.push(`A${rowIndex}:C${rowIndex}`);
      rowIndex++;
    }

    if (includeLegend) {
      rowIndex = this.appendLegendSection(rowsXml, mergeRefs, rowIndex, registry, hasAnyBlocked, hasAnySubtask);
    }

    const colWidths = includeSprintColumn
      ? [6, 12, 11, 46, 26, 20, 12, 16, 12, 20, 24]
      : [6, 12, 11, 50, 22, 12, 16, 12, 22, 26];

    return this.wrapWorksheetXml(rowsXml, mergeRefs, colWidths, headerRowIndex, lastColLetter, Math.max(headerRowIndex, rowIndex - 1));
  }

  // =========================================================================
  // SHEET THEO NHÓM SPRINT & BACKLOG (KHÔNG PHÁT SINH SPRINT RỖNG KHI LỌC)
  // =========================================================================

  private buildGroupedWorksheetXml(
    items: WorkItem[],
    sprints: Sprint[],
    projectName: string,
    title: string,
    subtitle: string,
    registry: StyleRegistry,
    includeLegend: boolean,
    includeBacklogSection: boolean
  ): string {
    const rowsXml: string[] = [];
    const mergeRefs: string[] = [];
    let rowIndex = 1;

    const headers = ['STT', 'Mã việc', 'Loại', 'Tên công việc', 'Epic', 'Ưu tiên', 'Trạng thái', 'Story Points', 'Người thực hiện', 'Ghi chú'];
    const lastColLetter = this.colLetter(headers.length - 1);

    rowsXml.push(`<row r="${rowIndex}" ht="26" customHeight="1">${this.strCell(`A${rowIndex}`, title, 1)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    const infoLine = `${subtitle || `Dự án: ${projectName}`}  |  Ngày xuất: ${new Date().toLocaleDateString('vi-VN')}`;
    rowsXml.push(`<row r="${rowIndex}" ht="20" customHeight="1">${this.strCell(`A${rowIndex}`, infoLine, 2)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    rowsXml.push(`<row r="${rowIndex}" ht="8" customHeight="1"/>`);
    rowIndex++;

    const headerRowIndex = rowIndex;
    rowsXml.push(
      `<row r="${rowIndex}" ht="24" customHeight="1">${headers
        .map((h, i) => this.strCell(`${this.colLetter(i)}${rowIndex}`, h, 3))
        .join('')}</row>`
    );
    rowIndex++;

    const itemsById = new Map(items.map((i) => [i.id.toLowerCase(), i]));
    const subtasksByParent = new Map<string, WorkItem[]>();
    for (const item of items) {
      if (item.parentId && itemsById.has(item.parentId.toLowerCase())) {
        const key = item.parentId.toLowerCase();
        const arr = subtasksByParent.get(key) ?? [];
        arr.push(item);
        subtasksByParent.set(key, arr);
      }
    }

    let globalStt = 0;
    let hasAnyBlocked = false;
    let hasAnySubtask = false;

    const appendSection = (sectionLabel: string, parentItems: WorkItem[]) => {
      const subItemsCount = parentItems.reduce(
        (sum, p) => sum + (subtasksByParent.get(p.id.toLowerCase())?.length || 0),
        0
      );
      const sectionSP = parentItems.reduce((s, p) => s + (p.storyPoints || 0), 0);
      const headerCells = headers
        .map((_, idx) =>
          this.strCell(
            `${this.colLetter(idx)}${rowIndex}`,
            idx === 0 ? `${sectionLabel} (${parentItems.length + subItemsCount} mục · ${sectionSP} SP)` : '',
            4
          )
        )
        .join('');
      rowsXml.push(`<row r="${rowIndex}" ht="22" customHeight="1">${headerCells}</row>`);
      mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
      rowIndex++;

      if (parentItems.length === 0) {
        rowsXml.push(
          `<row r="${rowIndex}" ht="20" customHeight="1">${this.strCell(
            `A${rowIndex}`,
            '(Chưa có công việc nào trong nhóm này)',
            13
          )}</row>`
        );
        mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
        rowIndex++;
        return;
      }

      for (const item of parentItems) {
        globalStt++;
        if (item.isBlocked) hasAnyBlocked = true;
        const stStyle = item.isBlocked
          ? registry.blockedStyleIndex
          : this.resolveStatusStyleIndex(item.statusName, registry);
        const stText = item.isBlocked ? `${item.statusName} (Bị chặn)` : item.statusName || 'To Do';
        const noteText = item.isBlocked ? `Bị chặn: ${item.blockReason || 'Đang vướng mắc'}` : (item.labels ?? []).join(', ');
        rowsXml.push(
          `<row r="${rowIndex}" ht="22" customHeight="1">${[
            this.numCell(`A${rowIndex}`, globalStt, 6),
            this.strCell(`B${rowIndex}`, item.issueKey || '', 7),
            this.strCell(`C${rowIndex}`, item.issueType || 'Task', 6),
            this.strCell(`D${rowIndex}`, item.title || '', 5),
            this.strCell(`E${rowIndex}`, item.epicName || '', 5),
            this.strCell(`F${rowIndex}`, item.priority || 'Medium', 6),
            this.strCell(`G${rowIndex}`, stText, stStyle),
            this.numCell(`H${rowIndex}`, item.storyPoints || 0, 8),
            this.strCell(`I${rowIndex}`, item.assigneeName || 'Chưa gán', 5),
            this.strCell(`J${rowIndex}`, noteText, item.isBlocked ? registry.blockedStyleIndex : 5),
          ].join('')}</row>`
        );
        rowIndex++;

        const subs = subtasksByParent.get(item.id.toLowerCase()) ?? [];
        for (const sub of subs) {
          globalStt++;
          hasAnySubtask = true;
          if (sub.isBlocked) hasAnyBlocked = true;
          const subStStyle = sub.isBlocked
            ? registry.blockedStyleIndex
            : this.resolveStatusStyleIndex(sub.statusName, registry);
          const subStText = sub.isBlocked ? `${sub.statusName} (Bị chặn)` : sub.statusName || 'To Do';
          rowsXml.push(
            `<row r="${rowIndex}" ht="20" customHeight="1">${[
              this.numCell(`A${rowIndex}`, globalStt, 10),
              this.strCell(`B${rowIndex}`, sub.issueKey || '', 10),
              this.strCell(`C${rowIndex}`, 'Việc con', 10),
              this.strCell(`D${rowIndex}`, `  ↳ ${sub.title || ''}`, 9),
              this.strCell(`E${rowIndex}`, '', 9),
              this.strCell(`F${rowIndex}`, sub.priority || 'Medium', 10),
              this.strCell(`G${rowIndex}`, subStText, subStStyle),
              this.numCell(`H${rowIndex}`, sub.storyPoints || 0, 8),
              this.strCell(`I${rowIndex}`, sub.assigneeName || 'Chưa gán', 9),
              this.strCell(`J${rowIndex}`, sub.subtaskRole || '', 9),
            ].join('')}</row>`
          );
          rowIndex++;
        }
      }
    };

    for (const sp of sprints) {
      const spParents = items.filter(
        (i) =>
          i.sprintId &&
          i.sprintId.toLowerCase() === sp.id.toLowerCase() &&
          (!i.parentId || !itemsById.has(i.parentId.toLowerCase())) &&
          (i.issueType || '').toLowerCase() !== 'epic'
      );
      const spStateVi = sp.status === 'active' ? 'Đang chạy' : sp.status === 'completed' ? 'Đã hoàn thành' : 'Sắp tới';
      appendSection(`Sprint: ${sp.name} [${spStateVi}]`, spParents);
    }

    if (includeBacklogSection) {
      const backlogParents = items.filter(
        (i) =>
          (!i.sprintId || i.sprintId === 'sp-backlog') &&
          (!i.parentId || !itemsById.has(i.parentId.toLowerCase())) &&
          (i.issueType || '').toLowerCase() !== 'epic'
      );
      appendSection('Kho Product Backlog', backlogParents);
    }

    if (includeLegend) {
      rowIndex = this.appendLegendSection(rowsXml, mergeRefs, rowIndex, registry, hasAnyBlocked, hasAnySubtask);
    }

    const colWidths = [6, 12, 11, 48, 22, 12, 16, 12, 22, 24];
    return this.wrapWorksheetXml(rowsXml, mergeRefs, colWidths, headerRowIndex, lastColLetter, Math.max(headerRowIndex, rowIndex - 1));
  }

  // =========================================================================
  // SHEET CHI TIẾT ĐẦY ĐỦ 1 CÔNG VIỆC (TASK DETAIL SHEET)
  // =========================================================================

  private buildTaskDetailWorksheetXml(
    task: WorkItem,
    subtasks: WorkItem[],
    projectName: string,
    title: string,
    registry: StyleRegistry,
    includeLegend: boolean,
    proposalTask?: ProposedTaskDto
  ): string {
    const rowsXml: string[] = [];
    const mergeRefs: string[] = [];
    let rowIndex = 1;
    const lastColLetter = 'F';

    rowsXml.push(`<row r="${rowIndex}" ht="26" customHeight="1">${this.strCell(`A${rowIndex}`, title, 1)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    rowsXml.push(
      `<row r="${rowIndex}" ht="20" customHeight="1">${this.strCell(
        `A${rowIndex}`,
        `Dự án: ${projectName}  |  Ngày xuất hồ sơ: ${new Date().toLocaleDateString('vi-VN')}`,
        2
      )}</row>`
    );
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    rowsXml.push(`<row r="${rowIndex}" ht="8" customHeight="1"/>`);
    rowIndex++;

    // Bảng 1: Thông tin tổng quan
    rowsXml.push(`<row r="${rowIndex}" ht="22" customHeight="1">${this.strCell(`A${rowIndex}`, '1. THÔNG TIN CHUNG CÔNG VIỆC', 3)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    const statusStyle = task.isBlocked
      ? registry.blockedStyleIndex
      : this.resolveStatusStyleIndex(task.statusName, registry);
    const statusText = task.isBlocked ? `${task.statusName} (Bị chặn)` : task.statusName || 'To Do';

    const pushKvRow = (k1: string, v1: string, k2: string, v2: string, v1Style = 5, v2Style = 5) => {
      rowsXml.push(
        `<row r="${rowIndex}" ht="22" customHeight="1">${[
          this.strCell(`A${rowIndex}`, k1, 12),
          this.strCell(`B${rowIndex}`, v1, v1Style),
          this.strCell(`C${rowIndex}`, '', v1Style),
          this.strCell(`D${rowIndex}`, k2, 12),
          this.strCell(`E${rowIndex}`, v2, v2Style),
          this.strCell(`F${rowIndex}`, '', v2Style),
        ].join('')}</row>`
      );
      mergeRefs.push(`B${rowIndex}:C${rowIndex}`);
      mergeRefs.push(`E${rowIndex}:F${rowIndex}`);
      rowIndex++;
    };

    pushKvRow('Mã công việc', task.issueKey || '-', 'Loại công việc', task.issueType || 'Story');
    pushKvRow('Trạng thái', statusText, 'Mức ưu tiên', task.priority || 'Medium', statusStyle, 5);
    pushKvRow('Sprint / Nguồn', task.sprintName || 'Kho Product Backlog', 'Epic', task.epicName || 'Chưa gán');
    pushKvRow(
      'Người thực hiện',
      task.assigneeName || 'Chưa gán',
      'Người báo cáo',
      task.reporterName || '-'
    );
    pushKvRow(
      'Story Points',
      `${task.storyPoints ?? 0} SP`,
      'Giờ ước lượng / Còn lại',
      `${task.estimatedHours ?? 0}h / ${task.remainingHours ?? task.estimatedHours ?? 0}h`
    );
    pushKvRow(
      'Nhãn / Kỹ năng',
      [...(task.labels ?? []), ...(task.requiredSkills ?? [])].join(', ') || 'Không có',
      'Cảnh báo / Ngày tạo',
      task.isBlocked
        ? `Bị chặn: ${task.blockReason || 'Đang vướng mắc'}`
        : task.createdAt
          ? new Date(task.createdAt).toLocaleDateString('vi-VN')
          : '-'
    );

    rowsXml.push(
      `<row r="${rowIndex}" ht="24" customHeight="1">${[
        this.strCell(`A${rowIndex}`, 'Tiêu đề đầy đủ', 12),
        this.strCell(`B${rowIndex}`, task.title || '', 5),
        this.strCell(`C${rowIndex}`, '', 5),
        this.strCell(`D${rowIndex}`, '', 5),
        this.strCell(`E${rowIndex}`, '', 5),
        this.strCell(`F${rowIndex}`, '', 5),
      ].join('')}</row>`
    );
    mergeRefs.push(`B${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    rowsXml.push(`<row r="${rowIndex}" ht="10" customHeight="1"/>`);
    rowIndex++;

    // Bảng 2: Mô tả chi tiết & Lý do đề xuất (nếu có)
    rowsXml.push(`<row r="${rowIndex}" ht="22" customHeight="1">${this.strCell(`A${rowIndex}`, '2. MÔ TẢ CHI TIẾT & ĐẶC TẢ', 3)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    const descText = (task.description || '').trim() || '(Chưa có mô tả chi tiết)';
    const descHt = Math.max(36, Math.min(180, 20 + Math.ceil(descText.length / 80) * 15));
    rowsXml.push(
      `<row r="${rowIndex}" ht="${descHt}" customHeight="1">${[
        this.strCell(`A${rowIndex}`, descText, 5),
        this.strCell(`B${rowIndex}`, '', 5),
        this.strCell(`C${rowIndex}`, '', 5),
        this.strCell(`D${rowIndex}`, '', 5),
        this.strCell(`E${rowIndex}`, '', 5),
        this.strCell(`F${rowIndex}`, '', 5),
      ].join('')}</row>`
    );
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    const rationaleText = (task.aiRationale || proposalTask?.aiRationale || '').trim();
    if (rationaleText) {
      rowsXml.push(
        `<row r="${rowIndex}" ht="28" customHeight="1">${[
          this.strCell(`A${rowIndex}`, 'Lý do / Đề xuất AI', 12),
          this.strCell(`B${rowIndex}`, rationaleText, 5),
          this.strCell(`C${rowIndex}`, '', 5),
          this.strCell(`D${rowIndex}`, '', 5),
          this.strCell(`E${rowIndex}`, '', 5),
          this.strCell(`F${rowIndex}`, '', 5),
        ].join('')}</row>`
      );
      mergeRefs.push(`B${rowIndex}:${lastColLetter}${rowIndex}`);
      rowIndex++;
    }

    rowsXml.push(`<row r="${rowIndex}" ht="10" customHeight="1"/>`);
    rowIndex++;

    // Bảng 3: Tiêu chí nghiệm thu (Acceptance Criteria)
    rowsXml.push(`<row r="${rowIndex}" ht="22" customHeight="1">${this.strCell(`A${rowIndex}`, '3. TIÊU CHÍ NGHIỆM THU (ACCEPTANCE CRITERIA)', 3)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    rowsXml.push(
      `<row r="${rowIndex}" ht="22" customHeight="1">${[
        this.strCell(`A${rowIndex}`, 'STT', 3),
        this.strCell(`B${rowIndex}`, 'Nội dung tiêu chí', 3),
        this.strCell(`C${rowIndex}`, '', 3),
        this.strCell(`D${rowIndex}`, '', 3),
        this.strCell(`E${rowIndex}`, '', 3),
        this.strCell(`F${rowIndex}`, 'Kết quả', 3),
      ].join('')}</row>`
    );
    mergeRefs.push(`B${rowIndex}:E${rowIndex}`);
    rowIndex++;

    const criteria = task.criteria ?? [];
    if (criteria.length === 0) {
      rowsXml.push(
        `<row r="${rowIndex}" ht="20" customHeight="1">${this.strCell(`A${rowIndex}`, '(Chưa có tiêu chí nghiệm thu)', 13)}</row>`
      );
      mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
      rowIndex++;
    } else {
      criteria.forEach((c, idx) => {
        rowsXml.push(
          `<row r="${rowIndex}" ht="22" customHeight="1">${[
            this.numCell(`A${rowIndex}`, idx + 1, 6),
            this.strCell(`B${rowIndex}`, c.content || '', 5),
            this.strCell(`C${rowIndex}`, '', 5),
            this.strCell(`D${rowIndex}`, '', 5),
            this.strCell(`E${rowIndex}`, '', 5),
            this.strCell(`F${rowIndex}`, c.isMet ? 'Đạt' : 'Chưa đạt', c.isMet ? this.resolveStatusStyleIndex('Done', registry) : 6),
          ].join('')}</row>`
        );
        mergeRefs.push(`B${rowIndex}:E${rowIndex}`);
        rowIndex++;
      });
    }

    rowsXml.push(`<row r="${rowIndex}" ht="10" customHeight="1"/>`);
    rowIndex++;

    // Bảng 4: Danh sách Việc con (Sub-tasks)
    rowsXml.push(`<row r="${rowIndex}" ht="22" customHeight="1">${this.strCell(`A${rowIndex}`, `4. DANH SÁCH VIỆC CON (${subtasks.length})`, 3)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    rowsXml.push(
      `<row r="${rowIndex}" ht="22" customHeight="1">${[
        this.strCell(`A${rowIndex}`, 'Mã việc', 3),
        this.strCell(`B${rowIndex}`, 'Tên việc con', 3),
        this.strCell(`C${rowIndex}`, 'Vai trò', 3),
        this.strCell(`D${rowIndex}`, 'Trạng thái', 3),
        this.strCell(`E${rowIndex}`, 'Người xử lý', 3),
        this.strCell(`F${rowIndex}`, 'Ước lượng', 3),
      ].join('')}</row>`
    );
    rowIndex++;

    let hasBlocked = !!task.isBlocked;
    if (subtasks.length === 0) {
      rowsXml.push(
        `<row r="${rowIndex}" ht="20" customHeight="1">${this.strCell(`A${rowIndex}`, '(Không có việc con trực thuộc)', 13)}</row>`
      );
      mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
      rowIndex++;
    } else {
      for (const sub of subtasks) {
        if (sub.isBlocked) hasBlocked = true;
        const subStStyle = sub.isBlocked
          ? registry.blockedStyleIndex
          : this.resolveStatusStyleIndex(sub.statusName, registry);
        rowsXml.push(
          `<row r="${rowIndex}" ht="22" customHeight="1">${[
            this.strCell(`A${rowIndex}`, sub.issueKey || '-', 7),
            this.strCell(`B${rowIndex}`, sub.title || '', 5),
            this.strCell(`C${rowIndex}`, sub.subtaskRole || sub.issueType || 'Sub-task', 6),
            this.strCell(`D${rowIndex}`, sub.isBlocked ? `${sub.statusName} (Bị chặn)` : sub.statusName || 'To Do', subStStyle),
            this.strCell(`E${rowIndex}`, sub.assigneeName || 'Chưa gán', 5),
            this.strCell(`F${rowIndex}`, `${sub.estimatedHours ?? 0}h / ${sub.storyPoints ?? 0} SP`, 6),
          ].join('')}</row>`
        );
        rowIndex++;
      }
    }

    // Bảng 5: Trích dẫn tài liệu & Bình luận (nếu có)
    const citations = [
      ...(task.documentCitations ?? []),
      ...(task.specLinks ?? []).map((l) => `${l.documentTitle || l.fileName || 'Spec'}${l.excerpt ? ` - ${l.excerpt}` : ''}`),
    ];
    const comments = task.comments ?? [];

    if (citations.length > 0 || comments.length > 0) {
      rowsXml.push(`<row r="${rowIndex}" ht="10" customHeight="1"/>`);
      rowIndex++;

      rowsXml.push(`<row r="${rowIndex}" ht="22" customHeight="1">${this.strCell(`A${rowIndex}`, '5. TÀI LIỆU CĂN CỨ & TRAO ĐỔI', 3)}</row>`);
      mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
      rowIndex++;

      for (const cite of citations) {
        rowsXml.push(
          `<row r="${rowIndex}" ht="20" customHeight="1">${[
            this.strCell(`A${rowIndex}`, 'Trích dẫn Spec', 12),
            this.strCell(`B${rowIndex}`, cite, 5),
            this.strCell(`C${rowIndex}`, '', 5),
            this.strCell(`D${rowIndex}`, '', 5),
            this.strCell(`E${rowIndex}`, '', 5),
            this.strCell(`F${rowIndex}`, '', 5),
          ].join('')}</row>`
        );
        mergeRefs.push(`B${rowIndex}:${lastColLetter}${rowIndex}`);
        rowIndex++;
      }

      for (const cm of comments) {
        const cmHeader = `${cm.authorName || 'Thành viên'} (${cm.createdAt ? new Date(cm.createdAt).toLocaleString('vi-VN') : ''})`;
        rowsXml.push(
          `<row r="${rowIndex}" ht="24" customHeight="1">${[
            this.strCell(`A${rowIndex}`, cmHeader, 12),
            this.strCell(`B${rowIndex}`, cm.content || '', 5),
            this.strCell(`C${rowIndex}`, '', 5),
            this.strCell(`D${rowIndex}`, '', 5),
            this.strCell(`E${rowIndex}`, '', 5),
            this.strCell(`F${rowIndex}`, '', 5),
          ].join('')}</row>`
        );
        mergeRefs.push(`B${rowIndex}:${lastColLetter}${rowIndex}`);
        rowIndex++;
      }
    }

    if (includeLegend) {
      rowIndex = this.appendLegendSection(rowsXml, mergeRefs, rowIndex, registry, hasBlocked, subtasks.length > 0);
    }

    const colWidths = [18, 38, 16, 18, 22, 18];
    return this.wrapWorksheetXml(rowsXml, mergeRefs, colWidths, 4, lastColLetter, rowIndex - 1);
  }

  // =========================================================================
  // SHEET BẢNG ĐỀ XUẤT AI (AI PROPOSAL EXPORT)
  // =========================================================================

  private buildProposalWorksheetXml(
    tasks: ProposedTaskDto[],
    proposal: AiSprintProposalDto | null | undefined,
    projectName: string,
    title: string,
    subtitle: string,
    registry: StyleRegistry,
    includeSubtasks: boolean,
    includeLegend: boolean
  ): string {
    const rowsXml: string[] = [];
    const mergeRefs: string[] = [];
    let rowIndex = 1;
    const headers = ['STT', 'Mã đề xuất', 'Loại', 'Tên công việc', 'Epic', 'Ưu tiên', 'Trạng thái', 'Story Points', 'Giờ ước lượng', 'Người phụ trách dự kiến', 'Tiêu chí nghiệm thu'];
    const lastColLetter = this.colLetter(headers.length - 1);

    rowsXml.push(`<row r="${rowIndex}" ht="26" customHeight="1">${this.strCell(`A${rowIndex}`, title, 1)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    const infoLine = subtitle || `Dự án: ${projectName} | Mục tiêu: ${proposal?.sprintGoal || 'Bảng đề xuất AI'} | Ngày xuất: ${new Date().toLocaleDateString('vi-VN')}`;
    rowsXml.push(`<row r="${rowIndex}" ht="20" customHeight="1">${this.strCell(`A${rowIndex}`, infoLine, 2)}</row>`);
    mergeRefs.push(`A${rowIndex}:${lastColLetter}${rowIndex}`);
    rowIndex++;

    rowsXml.push(`<row r="${rowIndex}" ht="8" customHeight="1"/>`);
    rowIndex++;

    const headerRowIndex = rowIndex;
    rowsXml.push(
      `<row r="${rowIndex}" ht="24" customHeight="1">${headers
        .map((h, i) => this.strCell(`${this.colLetter(i)}${rowIndex}`, h, 3))
        .join('')}</row>`
    );
    rowIndex++;

    let stt = 0;
    let totalSp = 0;
    let totalHrs = 0;
    let hasSubs = false;

    for (const task of tasks) {
      stt++;
      totalSp += task.storyPoints || 0;
      totalHrs += task.estimatedHours || 0;
      const stStyle = this.resolveStatusStyleIndex(task.status, registry);
      const acSummary = (task.acceptanceCriteria ?? []).join(' ; ');
      rowsXml.push(
        `<row r="${rowIndex}" ht="24" customHeight="1">${[
          this.numCell(`A${rowIndex}`, stt, 6),
          this.strCell(`B${rowIndex}`, task.sourceBacklogIssueKey || task.id || `T-${stt}`, 7),
          this.strCell(`C${rowIndex}`, task.type || 'Story', 6),
          this.strCell(`D${rowIndex}`, task.title || '', 5),
          this.strCell(`E${rowIndex}`, task.epicTitle || '', 5),
          this.strCell(`F${rowIndex}`, task.priority || 'Medium', 6),
          this.strCell(`G${rowIndex}`, task.status || 'To Do', stStyle),
          this.numCell(`H${rowIndex}`, task.storyPoints || 0, 8),
          this.numCell(`I${rowIndex}`, task.estimatedHours || 0, 8),
          this.strCell(`J${rowIndex}`, task.assigneeName || 'Chưa gán', 5),
          this.strCell(`K${rowIndex}`, acSummary, 5),
        ].join('')}</row>`
      );
      rowIndex++;

      if (includeSubtasks && task.subtasks && task.subtasks.length > 0) {
        hasSubs = true;
        for (const sub of task.subtasks) {
          stt++;
          const subStyle = this.resolveStatusStyleIndex(sub.status || task.status, registry);
          rowsXml.push(
            `<row r="${rowIndex}" ht="21" customHeight="1">${[
              this.numCell(`A${rowIndex}`, stt, 10),
              this.strCell(`B${rowIndex}`, sub.id || '', 10),
              this.strCell(`C${rowIndex}`, sub.role || 'Việc con', 10),
              this.strCell(`D${rowIndex}`, `  ↳ ${sub.title || ''}`, 9),
              this.strCell(`E${rowIndex}`, '', 9),
              this.strCell(`F${rowIndex}`, sub.priority || task.priority || 'Medium', 10),
              this.strCell(`G${rowIndex}`, sub.status || task.status || 'To Do', subStyle),
              this.numCell(`H${rowIndex}`, sub.storyPoints || 0, 8),
              this.numCell(`I${rowIndex}`, sub.estimatedHours || 0, 8),
              this.strCell(`J${rowIndex}`, sub.assigneeName || task.assigneeName || 'Chưa gán', 9),
              this.strCell(`K${rowIndex}`, (sub.acceptanceCriteria ?? []).join(' ; '), 9),
            ].join('')}</row>`
          );
          rowIndex++;
        }
      }
    }

    const sumCells = headers.map((_, idx) => {
      const ref = `${this.colLetter(idx)}${rowIndex}`;
      if (idx === 0) return this.strCell(ref, `Tổng cộng (${tasks.length} việc chính)`, 11);
      if (idx === 7) return this.numCell(ref, totalSp, 11);
      if (idx === 8) return this.numCell(ref, totalHrs, 11);
      return this.strCell(ref, '', 11);
    });
    rowsXml.push(`<row r="${rowIndex}" ht="22" customHeight="1">${sumCells.join('')}</row>`);
    mergeRefs.push(`A${rowIndex}:C${rowIndex}`);
    rowIndex++;

    if (includeLegend) {
      rowIndex = this.appendLegendSection(rowsXml, mergeRefs, rowIndex, registry, false, hasSubs);
    }

    const colWidths = [6, 14, 12, 46, 20, 12, 15, 12, 14, 22, 38];
    return this.wrapWorksheetXml(rowsXml, mergeRefs, colWidths, headerRowIndex, lastColLetter, rowIndex - 1);
  }

  // =========================================================================
  // BẢNG CHÚ THÍCH TRẠNG THÁI & KÝ HIỆU (LEGEND BLOCK)
  // =========================================================================

  private appendLegendSection(
    rowsXml: string[],
    mergeRefs: string[],
    startRow: number,
    registry: StyleRegistry,
    includeBlockedNote: boolean,
    includeSubtaskSymbol: boolean
  ): number {
    let rowIndex = startRow;
    rowsXml.push(`<row r="${rowIndex}" ht="12" customHeight="1"/>`);
    rowIndex++;

    rowsXml.push(
      `<row r="${rowIndex}" ht="22" customHeight="1">${[
        this.strCell(`A${rowIndex}`, 'CHÚ THÍCH TRẠNG THÁI & KÝ HIỆU', 14),
        this.strCell(`B${rowIndex}`, '', 14),
        this.strCell(`C${rowIndex}`, '', 14),
        this.strCell(`D${rowIndex}`, '', 14),
      ].join('')}</row>`
    );
    mergeRefs.push(`A${rowIndex}:D${rowIndex}`);
    rowIndex++;

    rowsXml.push(
      `<row r="${rowIndex}" ht="20" customHeight="1">${[
        this.strCell(`A${rowIndex}`, 'Màu / Ký hiệu', 3),
        this.strCell(`B${rowIndex}`, 'Ý nghĩa trong quy trình dự án', 3),
        this.strCell(`C${rowIndex}`, '', 3),
        this.strCell(`D${rowIndex}`, '', 3),
      ].join('')}</row>`
    );
    mergeRefs.push(`B${rowIndex}:D${rowIndex}`);
    rowIndex++;

    for (const entry of registry.legendEntries) {
      rowsXml.push(
        `<row r="${rowIndex}" ht="20" customHeight="1">${[
          this.strCell(`A${rowIndex}`, entry.name, entry.styleIndex),
          this.strCell(`B${rowIndex}`, entry.description, 5),
          this.strCell(`C${rowIndex}`, '', 5),
          this.strCell(`D${rowIndex}`, '', 5),
        ].join('')}</row>`
      );
      mergeRefs.push(`B${rowIndex}:D${rowIndex}`);
      rowIndex++;
    }

    if (includeBlockedNote) {
      rowsXml.push(
        `<row r="${rowIndex}" ht="20" customHeight="1">${[
          this.strCell(`A${rowIndex}`, 'Bị chặn (Blocked)', registry.blockedStyleIndex),
          this.strCell(`B${rowIndex}`, 'Công việc đang gặp vướng mắc cần xử lý gấp (nội dung chi tiết ở cột Ghi chú).', 5),
          this.strCell(`C${rowIndex}`, '', 5),
          this.strCell(`D${rowIndex}`, '', 5),
        ].join('')}</row>`
      );
      mergeRefs.push(`B${rowIndex}:D${rowIndex}`);
      rowIndex++;
    }

    if (includeSubtaskSymbol) {
      rowsXml.push(
        `<row r="${rowIndex}" ht="20" customHeight="1">${[
          this.strCell(`A${rowIndex}`, '  ↳ Tên việc', 9),
          this.strCell(`B${rowIndex}`, 'Ký hiệu Việc con (Sub-task) trực thuộc công việc cha nằm ngay phía trên.', 5),
          this.strCell(`C${rowIndex}`, '', 5),
          this.strCell(`D${rowIndex}`, '', 5),
        ].join('')}</row>`
      );
      mergeRefs.push(`B${rowIndex}:D${rowIndex}`);
      rowIndex++;
    }

    return rowIndex;
  }

  // =========================================================================
  // HELPERS TÍNH MÀU & CẤU TRÚC XML OPENXML
  // =========================================================================

  private wrapWorksheetXml(
    rowsXml: string[],
    mergeRefs: string[],
    colWidths: number[],
    freezeHeaderRow: number,
    autoFilterLastCol: string,
    autoFilterLastRow: number
  ): string {
    const colsXml = colWidths
      .map((w, idx) => `<col min="${idx + 1}" max="${idx + 1}" width="${w}" customWidth="1"/>`)
      .join('\n    ');

    const mergeXml =
      mergeRefs.length > 0
        ? `\n  <mergeCells count="${mergeRefs.length}">\n    ${mergeRefs
            .map((ref) => `<mergeCell ref="${ref}"/>`)
            .join('\n    ')}\n  </mergeCells>`
        : '';

    const autoFilterXml =
      autoFilterLastRow >= freezeHeaderRow
        ? `\n  <autoFilter ref="A${freezeHeaderRow}:${autoFilterLastCol}${autoFilterLastRow}"/>`
        : '';

    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheetViews>
    <sheetView workbookViewId="0">
      <pane ySplit="${freezeHeaderRow}" topLeftCell="A${freezeHeaderRow + 1}" activePane="bottomLeft" state="frozen"/>
    </sheetView>
  </sheetViews>
  <sheetFormatPr defaultRowHeight="20"/>
  <cols>
    ${colsXml}
  </cols>
  <sheetData>
    ${rowsXml.join('\n    ')}
  </sheetData>${autoFilterXml}${mergeXml}
  <pageSetup orientation="landscape" paperSize="9"/>
</worksheet>`;
  }

  private buildWorkbookXml(sheets: BuiltSheetSpec[]): string {
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
${sheets
  .map((s, idx) => `    <sheet name="${this.escapeXml(s.name)}" sheetId="${idx + 1}" r:id="rId${idx + 1}"/>`)
  .join('\n')}
  </sheets>
</workbook>`;
  }

  private resolveDefaultTitle(
    scope: ExcelExportScope,
    projectName: string,
    sprints: Sprint[],
    options: ExcelExportOptions
  ): string {
    switch (scope) {
      case 'single_sprint': {
        const spName =
          options.sprintNames?.[0] ||
          sprints.find((s) => this.eqId(s.id, options.sprintIds?.[0]))?.name ||
          'Sprint';
        return `BÁO CÁO CÔNG VIỆC - ${spName.toUpperCase()}`;
      }
      case 'sprint_list':
        return `BÁO CÁO CÁC SPRINT - ${projectName.toUpperCase()}`;
      case 'backlog_only':
        return `KHO PRODUCT BACKLOG - ${projectName.toUpperCase()}`;
      case 'single_task':
        return `CÔNG VIỆC ${options.issueKeys?.[0] || ''} - ${projectName.toUpperCase()}`.trim();
      case 'filtered_tasks':
        return `DANH SÁCH CÔNG VIỆC THEO BỘ LỌC - ${projectName.toUpperCase()}`;
      default:
        return `BÁO CÁO TỔNG HỢP DỰ ÁN - ${projectName.toUpperCase()}`;
    }
  }

  private buildDefaultSubtitle(
    scope: ExcelExportScope,
    projectName: string,
    sprints: Sprint[],
    options: ExcelExportOptions,
    count: number
  ): string {
    if (scope === 'single_sprint') {
      const spName =
        options.sprintNames?.[0] ||
        sprints.find((s) => this.eqId(s.id, options.sprintIds?.[0]))?.name ||
        'Sprint';
      return `Dự án: ${projectName} | Phạm vi: ${spName} (${count} mục)`;
    }
    if (scope === 'backlog_only') {
      return `Dự án: ${projectName} | Phạm vi: Chỉ kho Product Backlog (${count} mục)`;
    }
    return `Dự án: ${projectName} | Hệ thống ProjectMgmt`;
  }

  private resolveFocusedSheetName(
    scope: ExcelExportScope,
    sprints: Sprint[],
    options: ExcelExportOptions
  ): string {
    if (scope === 'single_sprint') {
      const spName =
        options.sprintNames?.[0] ||
        sprints.find((s) => this.eqId(s.id, options.sprintIds?.[0]))?.name ||
        'Sprint';
      return this.sanitizeSheetName(spName);
    }
    if (scope === 'backlog_only') return 'Kho Product Backlog';
    if (scope === 'single_task') {
      return this.sanitizeSheetName(options.issueKeys?.[0] ? `Công việc ${options.issueKeys[0]}` : 'Công việc');
    }
    return 'Danh sách Công việc';
  }

  private collectStatusNamesFromProposal(tasks: ProposedTaskDto[]): Array<{ name: string; color?: string }> {
    const out: Array<{ name: string; color?: string }> = [];
    for (const t of tasks) {
      if (t.status) out.push({ name: t.status });
      for (const s of t.subtasks ?? []) {
        if (s.status) out.push({ name: s.status });
      }
    }
    return out;
  }

  private mapProposedTaskToWorkItem(task: ProposedTaskDto, proposal?: AiSprintProposalDto | null): WorkItem {
    return {
      id: task.sourceBacklogIssueId || task.id,
      issueKey: task.sourceBacklogIssueKey || task.id,
      projectId: proposal?.projectId || '',
      epicId: task.epicId || undefined,
      epicName: task.epicTitle || undefined,
      title: task.title,
      description: task.description,
      statusId: '',
      statusName: task.status || proposal?.initialStatus || 'To Do',
      statusColor: '#64748B',
      isDone: false,
      issueType: task.type || 'Story',
      priority: (task.priority as any) || 'Medium',
      storyPoints: task.storyPoints || 0,
      estimatedHours: task.estimatedHours || 0,
      remainingHours: task.remainingHours || task.estimatedHours || 0,
      assigneeName: task.assigneeName || 'Chưa gán',
      reporterName: 'Trợ Lý AI',
      labels: task.tags ?? [],
      createdAt: new Date().toISOString(),
      requiredSkills: task.suggestedSkills ?? [],
      documentCitations: task.documentCitations ?? [],
      aiRationale: task.aiRationale || undefined,
      criteria: (task.acceptanceCriteria ?? []).map((c, idx) => ({
        id: `ac_${idx}`,
        content: c,
        isMet: false,
      })),
    };
  }

  private mapProposedSubtaskToWorkItem(
    sub: NonNullable<ProposedTaskDto['subtasks']>[number],
    parent: ProposedTaskDto,
    idx: number
  ): WorkItem {
    return {
      id: sub.id || `sub_${idx}`,
      issueKey: sub.id || `SUB-${idx + 1}`,
      projectId: '',
      parentId: parent.id,
      title: sub.title,
      description: sub.description || '',
      statusId: '',
      statusName: sub.status || parent.status || 'To Do',
      statusColor: '#64748B',
      isDone: false,
      issueType: 'Sub-task',
      subtaskRole: sub.role,
      priority: (sub.priority as any) || (parent.priority as any) || 'Medium',
      storyPoints: sub.storyPoints ?? 1,
      estimatedHours: sub.estimatedHours ?? 4,
      assigneeName: sub.assigneeName || parent.assigneeName || 'Chưa gán',
      reporterName: 'Trợ Lý AI',
      createdAt: new Date().toISOString(),
    };
  }

  private eqId(a?: string | null, b?: string | null): boolean {
    if (!a || !b) return false;
    return a.trim().toLowerCase() === b.trim().toLowerCase();
  }

  private normalizeHexColor(rawHex: string | undefined, category?: string): string {
    if (rawHex && /^#?[0-9a-fA-F]{6}$/.test(rawHex.trim())) {
      return rawHex.trim().replace('#', '').toUpperCase();
    }
    const cat = (category || '').toLowerCase();
    if (cat === 'done') return '16A34A';
    if (cat === 'inprogress') return '2563EB';
    if (cat === 'canceled') return '64748B';
    return '64748B';
  }

  private inferCategoryFromStatusName(lowerName: string): string {
    if (lowerName.includes('done') || lowerName.includes('hoàn thành') || lowerName.includes('closed') || lowerName.includes('xong')) {
      return 'Done';
    }
    if (lowerName.includes('cancel') || lowerName.includes('hủy') || lowerName.includes('rejected')) {
      return 'Canceled';
    }
    if (lowerName.includes('progress') || lowerName.includes('review') || lowerName.includes('test') || lowerName.includes('đang') || lowerName.includes('duyệt')) {
      return 'InProgress';
    }
    return 'ToDo';
  }

  private mixWithWhite(hex6: string, whiteRatio: number): string {
    const r = parseInt(hex6.slice(0, 2), 16);
    const g = parseInt(hex6.slice(2, 4), 16);
    const b = parseInt(hex6.slice(4, 6), 16);
    const mix = (c: number) => Math.round(c * (1 - whiteRatio) + 255 * whiteRatio);
    return [mix(r), mix(g), mix(b)]
      .map((v) => Math.max(0, Math.min(255, v)).toString(16).padStart(2, '0'))
      .join('')
      .toUpperCase();
  }

  private darkenColorForContrast(hex6: string): string {
    const r = parseInt(hex6.slice(0, 2), 16);
    const g = parseInt(hex6.slice(2, 4), 16);
    const b = parseInt(hex6.slice(4, 6), 16);
    const dark = (c: number) => Math.round(c * 0.48 + 15 * 0.52);
    return [dark(r), dark(g), dark(b)]
      .map((v) => Math.max(0, Math.min(255, v)).toString(16).padStart(2, '0'))
      .join('')
      .toUpperCase();
  }

  private sanitizeSheetName(raw: string): string {
    const cleaned = (raw || 'Bảng dữ liệu').replace(/[\\/?*[\]:]/g, ' ').replace(/\s+/g, ' ').trim();
    if (!cleaned) return 'Bảng dữ liệu';
    return cleaned.length > 31 ? cleaned.slice(0, 31).trim() : cleaned;
  }

  private sanitizeFilenameToken(raw: string): string {
    const clean = (raw || 'ProjectMgmt').replace(/[^\w\-]+/g, '_').replace(/^_+|_+$/g, '');
    return clean || 'ProjectMgmt';
  }

  private normalizeXlsxFilename(name: string): string {
    const trimmed = (name || 'ProjectMgmt_Bao_Cao.xlsx').trim();
    return trimmed.toLowerCase().endsWith('.xlsx') ? trimmed : `${trimmed}.xlsx`;
  }

  private colLetter(zeroBasedIndex: number): string {
    let n = zeroBasedIndex;
    let s = '';
    while (n >= 0) {
      s = String.fromCharCode((n % 26) + 65) + s;
      n = Math.floor(n / 26) - 1;
    }
    return s;
  }

  private strCell(ref: string, value: string, styleIdx: number): string {
    return `<c r="${ref}" t="inlineStr" s="${styleIdx}"><is><t xml:space="preserve">${this.escapeXml(value ?? '')}</t></is></c>`;
  }

  private numCell(ref: string, value: number, styleIdx: number): string {
    const num = Number.isFinite(value) ? value : 0;
    return `<c r="${ref}" t="n" s="${styleIdx}"><v>${num}</v></c>`;
  }

  private escapeXml(str: string): string {
    if (!str) return '';
    return String(str)
      .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g, '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;');
  }

  private utf8Bytes(text: string): Uint8Array {
    return new TextEncoder().encode(text);
  }

  private createZipArchive(entries: Array<{ name: string; data: Uint8Array }>): Uint8Array {
    const localParts: Uint8Array[] = [];
    const centralParts: Uint8Array[] = [];
    let offset = 0;

    for (const entry of entries) {
      const nameBytes = this.utf8Bytes(entry.name);
      const dataBytes = entry.data;
      const crc = this.crc32(dataBytes);

      const localHeader = new Uint8Array(30 + nameBytes.length);
      const localView = new DataView(localHeader.buffer);
      localView.setUint32(0, 0x04034b50, true);
      localView.setUint16(4, 20, true);
      localView.setUint16(6, 0x0800, true);
      localView.setUint16(8, 0, true);
      localView.setUint16(10, 0, true);
      localView.setUint16(12, 0x21, true);
      localView.setUint32(14, crc, true);
      localView.setUint32(18, dataBytes.length, true);
      localView.setUint32(22, dataBytes.length, true);
      localView.setUint16(26, nameBytes.length, true);
      localView.setUint16(28, 0, true);
      localHeader.set(nameBytes, 30);

      localParts.push(localHeader, dataBytes);

      const centralHeader = new Uint8Array(46 + nameBytes.length);
      const centralView = new DataView(centralHeader.buffer);
      centralView.setUint32(0, 0x02014b50, true);
      centralView.setUint16(4, 20, true);
      centralView.setUint16(6, 20, true);
      centralView.setUint16(8, 0x0800, true);
      centralView.setUint16(10, 0, true);
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
      centralView.setUint32(42, offset, true);
      centralHeader.set(nameBytes, 46);

      centralParts.push(centralHeader);
      offset += localHeader.length + dataBytes.length;
    }

    const centralSize = centralParts.reduce((sum, p) => sum + p.length, 0);
    const eocd = new Uint8Array(22);
    const eocdView = new DataView(eocd.buffer);
    eocdView.setUint32(0, 0x06054b50, true);
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
   * Tải tệp CSV mẫu nhập công việc hàng loạt.
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
      [headers.join(','), ...sampleRows.map((r) => r.map((c) => `"${c}"`).join(','))].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);

    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', 'ProjectMgmt_Task_Import_Template.csv');
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  /**
   * Phân tích nội dung tệp CSV nhập liệu thành các dòng công việc.
   */
  parseCsv(content: string): ImportedTaskRow[] {
    const lines = content.split(/\r?\n/).filter((line) => line.trim().length > 0);
    if (lines.length <= 1) return [];

    const results: ImportedTaskRow[] = [];

    for (let i = 1; i < lines.length; i++) {
      const line = lines[i];
      const matches = line.match(/(".*?"|[^",\s]+)(?=\s*,|\s*$)/g) || line.split(',');
      if (!matches || matches.length === 0) continue;

      const clean = matches.map((m) => m.replace(/^"|"$/g, '').trim());
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
