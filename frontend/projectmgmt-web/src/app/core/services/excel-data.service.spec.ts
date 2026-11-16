import { TestBed } from '@angular/core/testing';
import { describe, expect, it, beforeEach } from 'vitest';
import { ExcelDataService } from './excel-data.service';
import type { ProjectStatus, Sprint, WorkItem } from './project-management.service';
import type { AiSprintProposalDto } from './ai-agent.service';

const makeStatuses = (): ProjectStatus[] => [
  { id: 'st-todo', name: 'To Do', category: 'ToDo', colorHex: '#64748B', isInitial: true, orderIndex: 1 },
  { id: 'st-prog', name: 'In Progress', category: 'InProgress', colorHex: '#2563EB', isInitial: false, isActiveWork: true, orderIndex: 2 },
  { id: 'st-qa', name: 'QA Verify', category: 'InProgress', colorHex: '#7C3AED', isInitial: false, orderIndex: 3 },
  { id: 'st-done', name: 'Done', category: 'Done', colorHex: '#16A34A', isInitial: false, isDone: true, orderIndex: 4 },
];

const makeSprints = (): Sprint[] => [
  { id: 'sp-1', projectId: 'p-1', name: 'Sprint 1', status: 'completed', workItemsCount: 1, totalStoryPoints: 3 },
  { id: 'sp-2', projectId: 'p-1', name: 'Sprint 2', status: 'completed', workItemsCount: 1, totalStoryPoints: 5 },
  { id: 'sp-3', projectId: 'p-1', name: 'Sprint 3', goal: 'Hoàn thiện báo cáo Excel', status: 'active', workItemsCount: 2, totalStoryPoints: 8 },
];

const makeWorkItems = (): WorkItem[] => [
  {
    id: 'w-1',
    issueKey: 'QLDA-1',
    projectId: 'p-1',
    sprintId: 'sp-1',
    sprintName: 'Sprint 1',
    title: 'Khởi tạo hệ thống',
    statusId: 'st-done',
    statusName: 'Done',
    statusColor: '#16A34A',
    isDone: true,
    issueType: 'Story',
    priority: 'High',
    storyPoints: 3,
    assigneeName: 'Nguyễn Văn An',
    reporterName: 'Lead',
    createdAt: '2026-10-01T00:00:00Z',
  },
  {
    id: 'w-2',
    issueKey: 'QLDA-2',
    projectId: 'p-1',
    sprintId: 'sp-2',
    sprintName: 'Sprint 2',
    title: 'Xây dựng bảng Kanban',
    statusId: 'st-prog',
    statusName: 'In Progress',
    statusColor: '#2563EB',
    issueType: 'Task',
    priority: 'Medium',
    storyPoints: 5,
    assigneeName: 'Trần Thị Bình',
    reporterName: 'Lead',
    createdAt: '2026-10-05T00:00:00Z',
  },
  {
    id: 'w-3',
    issueKey: 'QLDA-3',
    projectId: 'p-1',
    sprintId: 'sp-3',
    sprintName: 'Sprint 3',
    epicName: 'Báo cáo Doanh nghiệp',
    title: 'Thiết kế API xuất báo cáo Excel',
    description: 'Cho phép xuất bảng sạch theo phạm vi Sprint hoặc Backlog.',
    statusId: 'st-qa',
    statusName: 'QA Verify',
    statusColor: '#7C3AED',
    issueType: 'Story',
    priority: 'Urgent',
    storyPoints: 5,
    estimatedHours: 12,
    remainingHours: 4,
    isBlocked: true,
    blockReason: 'Chờ xác nhận mẫu cột',
    assigneeName: 'Nguyễn Văn An',
    reporterName: 'Lead',
    labels: ['Backend', 'Export'],
    requiredSkills: ['.NET 8', 'OpenXML'],
    aiRationale: 'Chia nhỏ API để tối ưu dung lượng XML.',
    documentCitations: ['Spec_BaoCao.docx - Mục 3.1'],
    specLinks: [{ documentId: 'doc-1', documentTitle: 'Đặc tả Xuất Dữ Liệu', excerpt: 'Bảng sạch không màu nền đậm' }],
    criteria: [
      { id: 'c1', content: 'Xuất đúng Sprint 3 khi user chỉ định Sprint 3', isMet: true },
      { id: 'c2', content: 'Có bảng chú thích trạng thái ở cuối sheet', isMet: false },
    ],
    comments: [
      { id: 'cm1', authorName: 'Trần Thị Bình', content: 'Đã kiểm tra định dạng bảng chuẩn.', createdAt: '2026-10-12T08:30:00Z' },
    ],
    createdAt: '2026-10-10T00:00:00Z',
  },
  {
    id: 'w-4',
    issueKey: 'QLDA-4',
    projectId: 'p-1',
    parentId: 'w-3',
    sprintId: 'sp-3',
    sprintName: 'Sprint 3',
    title: 'Viết Unit Test bộ sinh OpenXML',
    statusId: 'st-todo',
    statusName: 'To Do',
    statusColor: '#64748B',
    issueType: 'Sub-task',
    subtaskRole: 'QA Engineer',
    priority: 'High',
    storyPoints: 3,
    estimatedHours: 6,
    assigneeName: 'Lê Minh Châu',
    reporterName: 'Lead',
    createdAt: '2026-10-11T00:00:00Z',
  },
  {
    id: 'w-5',
    issueKey: 'QLDA-5',
    projectId: 'p-1',
    sprintId: undefined,
    title: 'Nghiên cứu xuất PDF tổng quan',
    statusId: 'st-todo',
    statusName: 'To Do',
    statusColor: '#64748B',
    issueType: 'Story',
    priority: 'Low',
    storyPoints: 5,
    assigneeName: 'Chưa gán',
    reporterName: 'Lead',
    createdAt: '2026-10-12T00:00:00Z',
  },
  {
    id: 'w-6',
    issueKey: 'QLDA-6',
    projectId: 'p-1',
    sprintId: 'sp-backlog',
    title: 'Sửa lỗi hiển thị bộ lọc Backlog',
    statusId: 'st-prog',
    statusName: 'In Progress',
    statusColor: '#2563EB',
    issueType: 'Bug',
    priority: 'Urgent',
    storyPoints: 2,
    assigneeName: 'Nguyễn Văn An',
    reporterName: 'Lead',
    createdAt: '2026-10-13T00:00:00Z',
  },
];

describe('ExcelDataService - Xuất báo cáo bảng sạch chuẩn ProjectMgmt', () => {
  let service: ExcelDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ExcelDataService],
    });
    service = TestBed.inject(ExcelDataService);
  });

  it('uses clean neutral table styling with dynamic status colors, status legend, and zero Jira/AI dark banners', () => {
    const artifacts = service.buildWorkbookArtifacts(makeWorkItems(), makeSprints(), {
      scope: 'project_all',
      projectName: 'Hệ thống QLDA',
      projectKey: 'QLDA',
      projectStatuses: makeStatuses(),
    });

    expect(artifacts.filename.toLowerCase()).not.toContain('jira');
    expect(artifacts.filename).toMatch(/^QLDA_.*\.xlsx$/);
    expect(artifacts.sheets.map((s) => s.name)).toEqual(['Theo Sprint & Backlog', 'Danh sách Tổng hợp']);

    const allXml = [artifacts.workbookXml, artifacts.stylesXml, ...artifacts.sheets.map((s) => s.xml)].join('\n');
    expect(allXml.toLowerCase()).not.toContain('jira');
    // Không dùng banner tối màu #0F172A làm patternFill hay màu xanh Jira #0052CC hay nền tím Epic #F3E8FF
    expect(artifacts.stylesXml).not.toContain('fgColor rgb="FF0F172A"');
    expect(artifacts.stylesXml).not.toContain('0052CC');
    expect(artifacts.stylesXml).not.toContain('F3E8FF');
    // Header dùng nền xám nhạt thanh lịch #F1F5F9
    expect(artifacts.stylesXml).toContain('fgColor rgb="FFF1F5F9"');
    // Có màu pastel pha tự động cho trạng thái QA Verify (#7C3AED) và cột/bảng chú thích
    expect(artifacts.sheets[0].xml).toContain('CHÚ THÍCH TRẠNG THÁI');
    expect(artifacts.sheets[0].xml).toContain('QA Verify');
  });

  it('exports only Sprint 3 items when scope is single_sprint (excludes Sprint 1, Sprint 2, Backlog)', () => {
    const artifacts = service.buildWorkbookArtifacts(makeWorkItems(), makeSprints(), {
      scope: 'single_sprint',
      sprintIds: ['sp-3'],
      sprintNames: ['Sprint 3'],
      projectName: 'Hệ thống QLDA',
      projectKey: 'QLDA',
      projectStatuses: makeStatuses(),
      includeSubtasks: true,
    });

    expect(artifacts.itemCount).toBe(2);
    expect(artifacts.sheets.length).toBe(1);
    expect(artifacts.sheets[0].name).toBe('Sprint 3');

    const xml = artifacts.sheets[0].xml;
    expect(xml).toContain('QLDA-3');
    expect(xml).toContain('QLDA-4');
    expect(xml).not.toContain('QLDA-1');
    expect(xml).not.toContain('QLDA-2');
    expect(xml).not.toContain('QLDA-5');
    expect(xml).not.toContain('Sprint 1');
    expect(xml).not.toContain('Sprint 2');
  });

  it('exports multiple selected sprints when scope is sprint_list', () => {
    const artifacts = service.buildWorkbookArtifacts(makeWorkItems(), makeSprints(), {
      scope: 'sprint_list',
      sprintIds: ['sp-1', 'sp-3'],
      sprintNames: ['Sprint 1', 'Sprint 3'],
      projectName: 'Hệ thống QLDA',
      projectKey: 'QLDA',
      projectStatuses: makeStatuses(),
      includeSubtasks: true,
    });

    expect(artifacts.itemCount).toBe(3); // QLDA-1, QLDA-3, QLDA-4
    expect(artifacts.sheets.map((s) => s.name)).toEqual(['Danh sách Công việc', 'Theo từng Sprint']);
    const flatXml = artifacts.sheets[0].xml;
    expect(flatXml).toContain('QLDA-1');
    expect(flatXml).toContain('QLDA-3');
    expect(flatXml).not.toContain('QLDA-2');
    expect(flatXml).not.toContain('QLDA-5');
  });

  it('exports only Product Backlog items (both undefined sprintId and sp-backlog sentinel) when scope is backlog_only', () => {
    const artifacts = service.buildWorkbookArtifacts(makeWorkItems(), makeSprints(), {
      scope: 'backlog_only',
      projectName: 'Hệ thống QLDA',
      projectKey: 'QLDA',
      projectStatuses: makeStatuses(),
    });

    expect(artifacts.itemCount).toBe(2);
    expect(artifacts.sheets.length).toBe(1);
    expect(artifacts.sheets[0].name).toBe('Kho Product Backlog');

    const xml = artifacts.sheets[0].xml;
    expect(xml).toContain('QLDA-5');
    expect(xml).toContain('QLDA-6');
    expect(xml).not.toContain('QLDA-1');
    expect(xml).not.toContain('QLDA-3');
  });

  it('exports a single compact task row sheet when scope is single_task', () => {
    const artifacts = service.buildWorkbookArtifacts(makeWorkItems(), makeSprints(), {
      scope: 'single_task',
      issueKeys: ['QLDA-2'],
      includeSubtasks: true,
      projectName: 'Hệ thống QLDA',
      projectKey: 'QLDA',
      projectStatuses: makeStatuses(),
    });

    expect(artifacts.itemCount).toBe(1);
    expect(artifacts.sheets.length).toBe(1);
    expect(artifacts.sheets[0].xml).toContain('QLDA-2');
    expect(artifacts.sheets[0].xml).not.toContain('QLDA-3');
    expect(artifacts.sheets[0].xml).not.toContain('QLDA-5');
  });

  it('exports structured task_detail dossier (Overview, Description, Acceptance Criteria, Sub-tasks, Citations, Comments, Legend)', () => {
    const artifacts = service.buildWorkbookArtifacts(makeWorkItems(), makeSprints(), {
      scope: 'task_detail',
      issueKeys: ['QLDA-3'],
      projectName: 'Hệ thống QLDA',
      projectKey: 'QLDA',
      projectStatuses: makeStatuses(),
    });

    expect(artifacts.itemCount).toBe(1);
    expect(artifacts.sheets.length).toBe(1);
    expect(artifacts.sheets[0].name).toContain('QLDA-3');

    const xml = artifacts.sheets[0].xml;
    expect(xml).toContain('1. THÔNG TIN CHUNG CÔNG VIỆC');
    expect(xml).toContain('2. MÔ TẢ CHI TIẾT');
    expect(xml).toContain('3. TIÊU CHÍ NGHIỆM THU');
    expect(xml).toContain('4. DANH SÁCH VIỆC CON');
    expect(xml).toContain('5. TÀI LIỆU CĂN CỨ');
    expect(xml).toContain('QLDA-4');
    expect(xml).toContain('Viết Unit Test bộ sinh OpenXML');
    expect(xml).toContain('Spec_BaoCao.docx - Mục 3.1');
    expect(xml).toContain('Đặc tả Xuất Dữ Liệu - Bảng sạch không màu nền đậm');
    expect(xml).toContain('Đã kiểm tra định dạng bảng chuẩn.');
    expect(xml).toContain('CHÚ THÍCH TRẠNG THÁI');
  });

  it('filters accurately for filtered_tasks (Bug / Blocked / Assignee)', () => {
    const artifacts = service.buildWorkbookArtifacts(makeWorkItems(), makeSprints(), {
      scope: 'filtered_tasks',
      assigneeNames: ['Nguyễn Văn An'],
      blockedOnly: true,
      projectName: 'Hệ thống QLDA',
      projectKey: 'QLDA',
      projectStatuses: makeStatuses(),
      flatOnly: true,
    });

    expect(artifacts.itemCount).toBe(1);
    expect(artifacts.sheets[0].xml).toContain('QLDA-3');
    expect(artifacts.sheets[0].xml).not.toContain('QLDA-1');
    expect(artifacts.sheets[0].xml).not.toContain('QLDA-6');
  });

  it('exports ai_proposal draft and single task_detail inside ai_proposal', () => {
    const proposal: AiSprintProposalDto = {
      sessionId: 'sprint_ai_1',
      projectId: 'p-1',
      sprintGoal: 'Phát triển tính năng phân rã thông minh',
      rationale: 'Tối ưu tiến độ Sprint 4',
      totalEstimatedHours: 10,
      totalStoryPoints: 5,
      tasks: [
        {
          id: 'task_01',
          title: 'Xây dựng API đồng bộ dữ liệu',
          description: 'Triển khai endpoint REST và đồng bộ trạng thái.',
          type: 'Story',
          priority: 'High',
          status: 'To Do',
          isReady: true,
          storyPoints: 5,
          estimatedHours: 10,
          remainingHours: 10,
          assigneeName: 'Nguyễn Văn An',
          suggestedSkills: ['.NET', 'OpenXML'],
          acceptanceCriteria: ['Phản hồi dưới 200ms', 'Ghi log đầy đủ'],
          documentCitations: ['Spec.docx - Mục 2'],
          subtasks: [
            { id: 'sub_01', title: 'Tạo Controller', role: 'Backend', storyPoints: 2, estimatedHours: 4, status: 'To Do' },
          ],
        },
      ],
    };

    const fullDraft = service.buildWorkbookArtifacts([], [], {
      scope: 'ai_proposal',
      proposal,
      projectName: 'Hệ thống QLDA',
      projectKey: 'QLDA',
      projectStatuses: makeStatuses(),
      includeSubtasks: true,
    });
    expect(fullDraft.itemCount).toBe(2);
    expect(fullDraft.sheets[0].name).toBe('Đề xuất AI');
    expect(fullDraft.sheets[0].xml).toContain('Xây dựng API đồng bộ dữ liệu');
    expect(fullDraft.sheets[0].xml).toContain('Tạo Controller');

    const draftDetail = service.buildWorkbookArtifacts([], [], {
      scope: 'task_detail',
      searchKeyword: 'ai_proposal',
      issueKeys: ['task_01'],
      proposal,
      projectName: 'Hệ thống QLDA',
      projectKey: 'QLDA',
      projectStatuses: makeStatuses(),
    });
    expect(draftDetail.itemCount).toBe(1);
    expect(draftDetail.sheets[0].xml).toContain('Phản hồi dưới 200ms');
    expect(draftDetail.sheets[0].xml).toContain('Tạo Controller');
  });
});
