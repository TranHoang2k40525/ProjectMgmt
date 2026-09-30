import { ApiResult } from './account-api.models';

export interface ProfileDto extends ApiResult {
  userId?: string | null;
  email?: string | null;
  fullName?: string | null;
  avatarUrl?: string | null;
  phoneNumber?: string | null;
  bio?: string | null;
  timezone?: string | null;
  jobTitle?: string | null;
  seniorityLevel?: string | null;
  yearsOfExperience?: number | null;
  updatedAt?: string | null;
  skills?: SkillDto[] | null;
}

export interface AvatarResult extends ApiResult {
  avatarUrl?: string | null;
}

export interface SkillDto extends ApiResult {
  userId?: string | null;
  skillId?: string | null;
  code?: string | null;
  name?: string | null;
  category?: string | null;
  isActive?: boolean | null;
  proficiencyLevel?: number | null;
  level?: number | null;
  yearsOfExperience?: number | null;
  isSelfDeclared?: boolean | null;
  verified?: boolean | null;
  updatedCount?: number | null;
  items?: SkillDto[] | null;
  skills?: SkillDto[] | null;
}

export interface ForYouDto extends ApiResult {
  issueId?: string | null;
  projectId?: string | null;
  issueKey?: string | null;
  title?: string | null;
  statusName?: string | null;
  dueDate?: string | null;
  assignedIssues?: ForYouDto[] | null;
  recentIssues?: ForYouDto[] | null;
  attentionFocus?: string[] | null;
}
