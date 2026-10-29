import {
  IssueTypeNameSetting,
  ManagementType,
  OrgRoleCode,
  ProjectRoleCode,
  RecentKind,
  StatusCategory,
  WorkItemSummary,
  WorkflowStatusDef,
  WorkspaceUser
} from './workspace.models';

/** Các "bảng" mà mock giả lập; lưu nguyên khối vào localStorage theo người dùng. */
export interface MockWorkspaceDb {
  version: 1;
  users: WorkspaceUser[];
  organizations: { id: string; name: string; slug: string; ownerId: string; defaultTemplateId: string; createdAt: string }[];
  orgMembers: { orgId: string; userId: string; role: OrgRoleCode; joinedAt: string }[];
  invitations: {
    id: string;
    orgId: string;
    email: string;
    role: OrgRoleCode;
    invitedBy: string;
    createdAt: string;
    status: 'Pending' | 'Accepted' | 'Declined' | 'Cancelled';
  }[];
  inviteLinks: { token: string; orgId: string; role: OrgRoleCode; createdBy: string; createdAt: string; isActive: boolean }[];
  workflowTemplates: { id: string; orgId: string; name: string; managementType: ManagementType; statuses: WorkflowStatusDef[] }[];
  projects: {
    id: string;
    orgId: string;
    projectKey: string;
    name: string;
    description: string | null;
    leadUserId: string;
    workflowTemplateId: string;
    workflowTemplateName: string;
    managementType: ManagementType;
    statuses: WorkflowStatusDef[];
    issueCounter: number;
    createdAt: string;
  }[];
  projectMembers: { projectId: string; userId: string; role: ProjectRoleCode }[];
  issueTypes: { projectId: string; code: IssueTypeNameSetting['code']; name: string }[];
  workItems: {
    id: string;
    projectId: string;
    number: number;
    title: string;
    assigneeId: string | null;
    statusName: string;
    statusCategory: StatusCategory;
    priority: WorkItemSummary['priority'];
    dueDate: string | null;
  }[];
  recentVisits: { kind: RecentKind; id: string; visitedAt: string }[];
}

const todo = (name: string): WorkflowStatusDef => ({ name, category: 'ToDo' });
const doing = (name: string): WorkflowStatusDef => ({ name, category: 'InProgress' });
const done = (name: string): WorkflowStatusDef => ({ name, category: 'Done' });

/** Mẫu workflow có sẵn của hệ thống — cùng tên trạng thái với ProjectCustomizationController. */
export const PRESET_WORKFLOW_TEMPLATES: readonly {
  id: string;
  name: string;
  managementType: ManagementType;
  statuses: WorkflowStatusDef[];
}[] = [
  { id: 'preset-scrum-std', name: 'Scrum chuẩn', managementType: 'Scrum', statuses: [todo('To Do'), doing('In Progress'), doing('Code Review'), done('Done')] },
  { id: 'preset-agile-qa', name: 'Agile + QA', managementType: 'Scrum', statuses: [todo('To Do'), doing('In Progress'), doing('Code Review'), doing('QA Testing'), done('Done')] },
  { id: 'preset-kanban-simple', name: 'Kanban đơn giản', managementType: 'Kanban', statuses: [todo('To Do'), doing('In Progress'), done('Done')] },
  {
    id: 'preset-enterprise',
    name: 'Enterprise',
    managementType: 'Scrum',
    statuses: [todo('Backlog'), doing('Spec Review'), doing('In Progress'), doing('Code Review'), doing('QA Verification'), done('Done')]
  }
];

export const SAMPLE_USERS: readonly WorkspaceUser[] = [
  { id: 'mock-user-01', displayName: 'Nguyễn Minh Anh', email: 'minhanh.nguyen@example.com', avatarUrl: null },
  { id: 'mock-user-02', displayName: 'Trần Quốc Bảo', email: 'quocbao.tran@example.com', avatarUrl: null },
  { id: 'mock-user-03', displayName: 'Lê Thu Hà', email: 'thuha.le@example.com', avatarUrl: null },
  { id: 'mock-user-04', displayName: 'Phạm Đức Khang', email: 'duckhang.pham@example.com', avatarUrl: null },
  { id: 'mock-user-05', displayName: 'Võ Ngọc Linh', email: 'ngoclinh.vo@example.com', avatarUrl: null },
  { id: 'mock-user-06', displayName: 'Đặng Hải Nam', email: 'hainam.dang@example.com', avatarUrl: null },
  { id: 'mock-user-07', displayName: 'Bùi Thanh Phương', email: 'thanhphuong.bui@example.com', avatarUrl: null },
  { id: 'mock-user-08', displayName: 'Hoàng Gia Quân', email: 'giaquan.hoang@example.com', avatarUrl: null }
];

const DAY = 86_400_000;

function dayOffset(now: number, days: number): string {
  const date = new Date(now + days * DAY);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

function iso(now: number, days: number): string {
  return new Date(now + days * DAY).toISOString();
}

/**
 * Dữ liệu mẫu cho lần đầu người dùng mở không gian làm việc:
 * một tổ chức mình làm chủ, một tổ chức mình là thành viên, một lời mời đang chờ, công việc có hạn khác nhau.
 */
export function createWorkspaceSeed(me: WorkspaceUser, now: number = Date.now()): MockWorkspaceDb {
  const [anh, bao, ha, khang, linh] = SAMPLE_USERS;
  const labId = 'mock-org-lab';
  const thesisId = 'mock-org-thesis';
  const clubId = 'mock-org-club';
  const portalId = 'mock-project-portal';
  const bookingId = 'mock-project-booking';
  const thesisProjectId = 'mock-project-thesis';
  const labTemplateId = 'mock-template-lab-review';
  const labStatuses = [todo('Backlog'), todo('To Do'), doing('In Progress'), doing('Peer Review'), done('Done')];
  const scrum = PRESET_WORKFLOW_TEMPLATES[0];
  const kanban = PRESET_WORKFLOW_TEMPLATES[2];

  const issueTypesFor = (projectId: string) =>
    (['Epic', 'Story', 'Task', 'Bug', 'Subtask'] as const).map(code => ({ projectId, code, name: code }));

  return {
    version: 1,
    users: [me, ...SAMPLE_USERS.filter(user => user.id !== me.id)],
    organizations: [
      { id: labId, name: 'HUCE Software Lab', slug: 'huce-software-lab', ownerId: me.id, defaultTemplateId: labTemplateId, createdAt: iso(now, -60) },
      { id: thesisId, name: 'Nhóm Đồ án Tốt nghiệp', slug: 'nhom-do-an-tot-nghiep', ownerId: anh.id, defaultTemplateId: scrum.id, createdAt: iso(now, -45) },
      { id: clubId, name: 'CLB Lập trình HUCE', slug: 'clb-lap-trinh-huce', ownerId: bao.id, defaultTemplateId: kanban.id, createdAt: iso(now, -30) }
    ],
    orgMembers: [
      { orgId: labId, userId: me.id, role: 'Owner', joinedAt: iso(now, -60) },
      { orgId: labId, userId: ha.id, role: 'Admin', joinedAt: iso(now, -50) },
      { orgId: labId, userId: khang.id, role: 'Member', joinedAt: iso(now, -40) },
      { orgId: labId, userId: linh.id, role: 'Member', joinedAt: iso(now, -20) },
      { orgId: thesisId, userId: anh.id, role: 'Owner', joinedAt: iso(now, -45) },
      { orgId: thesisId, userId: me.id, role: 'Member', joinedAt: iso(now, -44) },
      { orgId: thesisId, userId: bao.id, role: 'Member', joinedAt: iso(now, -40) },
      { orgId: clubId, userId: bao.id, role: 'Owner', joinedAt: iso(now, -30) }
    ],
    invitations: [
      { id: 'mock-invite-club', orgId: clubId, email: me.email.toLowerCase(), role: 'Member', invitedBy: bao.id, createdAt: iso(now, -1), status: 'Pending' },
      { id: 'mock-invite-lab', orgId: labId, email: SAMPLE_USERS[5].email, role: 'Member', invitedBy: me.id, createdAt: iso(now, -2), status: 'Pending' }
    ],
    inviteLinks: [
      { token: 'lab-join-7f3k2q9xw1', orgId: labId, role: 'Member', createdBy: me.id, createdAt: iso(now, -10), isActive: true },
      { token: 'thesis-join-4m8p6r2t0z', orgId: thesisId, role: 'Member', createdBy: anh.id, createdAt: iso(now, -9), isActive: true }
    ],
    workflowTemplates: [{ id: labTemplateId, orgId: labId, name: 'Scrum + Review nội bộ', managementType: 'Scrum', statuses: labStatuses }],
    projects: [
      {
        id: portalId, orgId: labId, projectKey: 'CTSV', name: 'Cổng thông tin sinh viên', description: 'Cổng tra cứu điểm, lịch học và học phí.',
        leadUserId: me.id, workflowTemplateId: labTemplateId, workflowTemplateName: 'Scrum + Review nội bộ', managementType: 'Scrum',
        statuses: labStatuses, issueCounter: 6, createdAt: iso(now, -40)
      },
      {
        id: bookingId, orgId: labId, projectKey: 'DPHT', name: 'Đặt phòng hội thảo', description: null,
        leadUserId: ha.id, workflowTemplateId: kanban.id, workflowTemplateName: kanban.name, managementType: 'Kanban',
        statuses: kanban.statuses, issueCounter: 3, createdAt: iso(now, -15)
      },
      {
        id: thesisProjectId, orgId: thesisId, projectKey: 'QLDA', name: 'Quản lý dự án Scrum tích hợp AI', description: 'Đồ án tốt nghiệp nhóm 7.',
        leadUserId: anh.id, workflowTemplateId: scrum.id, workflowTemplateName: scrum.name, managementType: 'Scrum',
        statuses: scrum.statuses, issueCounter: 4, createdAt: iso(now, -44)
      }
    ],
    projectMembers: [
      { projectId: portalId, userId: me.id, role: 'ProjectManager' },
      { projectId: portalId, userId: ha.id, role: 'ScrumMaster' },
      { projectId: portalId, userId: khang.id, role: 'Developer' },
      { projectId: bookingId, userId: ha.id, role: 'ProjectManager' },
      { projectId: bookingId, userId: me.id, role: 'Developer' },
      { projectId: thesisProjectId, userId: anh.id, role: 'ProjectManager' },
      { projectId: thesisProjectId, userId: me.id, role: 'Developer' },
      { projectId: thesisProjectId, userId: bao.id, role: 'Developer' }
    ],
    issueTypes: [...issueTypesFor(portalId), ...issueTypesFor(bookingId), ...issueTypesFor(thesisProjectId)],
    workItems: [
      { id: 'mock-issue-01', projectId: portalId, number: 1, title: 'Thiết kế màn tra cứu điểm', assigneeId: me.id, statusName: 'In Progress', statusCategory: 'InProgress', priority: 'High', dueDate: dayOffset(now, -2) },
      { id: 'mock-issue-02', projectId: portalId, number: 2, title: 'API lịch học theo tuần', assigneeId: me.id, statusName: 'Peer Review', statusCategory: 'InProgress', priority: 'Medium', dueDate: dayOffset(now, 0) },
      { id: 'mock-issue-03', projectId: portalId, number: 3, title: 'Xuất hóa đơn học phí PDF', assigneeId: me.id, statusName: 'To Do', statusCategory: 'ToDo', priority: 'Medium', dueDate: dayOffset(now, 3) },
      { id: 'mock-issue-04', projectId: portalId, number: 4, title: 'Kiểm thử đăng nhập SSO', assigneeId: khang.id, statusName: 'To Do', statusCategory: 'ToDo', priority: 'Low', dueDate: dayOffset(now, 5) },
      { id: 'mock-issue-05', projectId: portalId, number: 5, title: 'Tối ưu truy vấn bảng điểm', assigneeId: me.id, statusName: 'Backlog', statusCategory: 'ToDo', priority: 'Low', dueDate: null },
      { id: 'mock-issue-06', projectId: portalId, number: 6, title: 'Sửa lỗi hiển thị lịch thi', assigneeId: me.id, statusName: 'Done', statusCategory: 'Done', priority: 'High', dueDate: dayOffset(now, -5) },
      { id: 'mock-issue-07', projectId: bookingId, number: 1, title: 'Form đặt phòng theo khung giờ', assigneeId: me.id, statusName: 'In Progress', statusCategory: 'InProgress', priority: 'Highest', dueDate: dayOffset(now, 1) },
      { id: 'mock-issue-08', projectId: bookingId, number: 2, title: 'Thông báo xác nhận qua email', assigneeId: ha.id, statusName: 'To Do', statusCategory: 'ToDo', priority: 'Medium', dueDate: dayOffset(now, 6) },
      { id: 'mock-issue-09', projectId: thesisProjectId, number: 1, title: 'Viết chương 3 — thiết kế hệ thống', assigneeId: me.id, statusName: 'In Progress', statusCategory: 'InProgress', priority: 'High', dueDate: dayOffset(now, -1) },
      { id: 'mock-issue-10', projectId: thesisProjectId, number: 2, title: 'Chuẩn bị slide bảo vệ', assigneeId: me.id, statusName: 'To Do', statusCategory: 'ToDo', priority: 'Medium', dueDate: dayOffset(now, 10) },
      { id: 'mock-issue-11', projectId: thesisProjectId, number: 3, title: 'Đánh giá mô hình AI phân rã', assigneeId: bao.id, statusName: 'To Do', statusCategory: 'ToDo', priority: 'Medium', dueDate: dayOffset(now, 7) }
    ],
    recentVisits: [
      { kind: 'issue', id: 'mock-issue-02', visitedAt: iso(now, -0.05) },
      { kind: 'project', id: portalId, visitedAt: iso(now, -0.1) },
      { kind: 'organization', id: labId, visitedAt: iso(now, -0.2) },
      { kind: 'project', id: thesisProjectId, visitedAt: iso(now, -1) },
      { kind: 'organization', id: thesisId, visitedAt: iso(now, -1.1) }
    ]
  };
}
