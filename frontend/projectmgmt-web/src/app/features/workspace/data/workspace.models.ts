export type OrgRoleCode = 'Owner' | 'Admin' | 'Member';
export type ProjectRoleCode = 'ProjectManager' | 'ScrumMaster' | 'ProductOwner' | 'Developer' | 'Viewer';
export type StatusCategory = 'ToDo' | 'InProgress' | 'Done';
export type ManagementType = 'Scrum' | 'Kanban';
export type IssueTypeCode = 'Epic' | 'Story' | 'Task' | 'Bug' | 'Subtask';
export type RecentKind = 'organization' | 'project' | 'issue';
export type InviteEmailOutcome = 'Sent' | 'NoAccount' | 'AlreadyMember' | 'AlreadyInvited';

export interface WorkspaceUser {
  id: string;
  displayName: string;
  email: string;
  avatarUrl: string | null;
}

export interface OrganizationSummary {
  id: string;
  name: string;
  slug: string;
  ownerId: string;
  ownerName: string;
  myRole: OrgRoleCode;
  projectCount: number;
  memberCount: number;
  createdAt: string;
}

export interface CreateOrganizationRequest {
  name: string;
  slug: string;
}

export interface OrgMember {
  userId: string;
  displayName: string;
  email: string;
  avatarUrl: string | null;
  role: OrgRoleCode;
  joinedAt: string;
}

export interface OrgRoleOption {
  code: OrgRoleCode;
  name: string;
  description: string;
  assignable: boolean;
}

export interface OrgInvitation {
  id: string;
  orgId: string;
  email: string;
  role: OrgRoleCode;
  invitedByName: string;
  createdAt: string;
}

export interface MyInvitation {
  id: string;
  orgId: string;
  orgName: string;
  role: OrgRoleCode;
  invitedByName: string;
  createdAt: string;
}

export interface InviteEmailResult {
  email: string;
  outcome: InviteEmailOutcome;
}

export interface InviteLink {
  token: string;
  orgId: string;
  role: OrgRoleCode;
  createdAt: string;
}

export interface InviteLinkPreview {
  token: string;
  orgId: string;
  orgName: string;
  invitedByName: string;
  role: OrgRoleCode;
  alreadyMember: boolean;
}

export interface WorkflowStatusDef {
  name: string;
  category: StatusCategory;
}

export interface WorkflowTemplate {
  id: string;
  /** null với mẫu có sẵn của hệ thống. */
  orgId: string | null;
  name: string;
  managementType: ManagementType;
  statuses: WorkflowStatusDef[];
  isPreset: boolean;
  isDefault: boolean;
}

export interface SaveWorkflowTemplateRequest {
  name: string;
  managementType: ManagementType;
  statuses: WorkflowStatusDef[];
}

export interface WorkspaceProject {
  id: string;
  orgId: string;
  projectKey: string;
  name: string;
  description: string | null;
  leadUserId: string;
  leadName: string;
  workflowTemplateId: string;
  workflowTemplateName: string;
  managementType: ManagementType;
  memberCount: number;
  createdAt: string;
}

export interface IssueTypeNameSetting {
  code: IssueTypeCode;
  name: string;
}

export interface ProjectMemberSelection {
  userId: string;
  role: ProjectRoleCode;
}

export interface CreateProjectRequest {
  name: string;
  projectKey: string;
  description: string | null;
  leadUserId: string;
  workflowTemplateId: string;
  issueTypes: IssueTypeNameSetting[];
  members: ProjectMemberSelection[];
}

export interface WorkItemSummary {
  id: string;
  issueKey: string;
  title: string;
  projectId: string;
  projectName: string;
  orgId: string;
  orgName: string;
  statusName: string;
  statusCategory: StatusCategory;
  priority: 'Low' | 'Medium' | 'High' | 'Highest';
  dueDate: string | null;
}

export interface RecentItem {
  kind: RecentKind;
  id: string;
  title: string;
  subtitle: string;
  orgId: string;
  visitedAt: string;
}

export interface RecordVisitRequest {
  kind: RecentKind;
  id: string;
}

export const DEFAULT_ISSUE_TYPES: readonly IssueTypeNameSetting[] = [
  { code: 'Epic', name: 'Epic' },
  { code: 'Story', name: 'Story' },
  { code: 'Task', name: 'Task' },
  { code: 'Bug', name: 'Bug' },
  { code: 'Subtask', name: 'Subtask' }
];

export const PROJECT_ROLE_OPTIONS: readonly { code: ProjectRoleCode; name: string }[] = [
  { code: 'ProjectManager', name: 'Quản lý dự án' },
  { code: 'ScrumMaster', name: 'Scrum Master' },
  { code: 'ProductOwner', name: 'Product Owner' },
  { code: 'Developer', name: 'Developer' },
  { code: 'Viewer', name: 'Chỉ xem' }
];

export const STATUS_CATEGORY_LABELS: Readonly<Record<StatusCategory, string>> = {
  ToDo: 'Cần làm',
  InProgress: 'Đang làm',
  Done: 'Hoàn thành'
};

export const ORG_ROLE_LABELS: Readonly<Record<OrgRoleCode, string>> = {
  Owner: 'Chủ tổ chức',
  Admin: 'Quản trị',
  Member: 'Thành viên'
};
