export interface UserProfileModel {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  jobTitle: string;
  roleId: string;
  roleName: string;
  isActive: boolean;
  twoFactorEnabled: boolean;
  createdAt: string;
  lastLoginAt: string;
  phoneNumber?: string | null;
  bio?: string | null;
  timezone?: string | null;
  seniorityLevel?: string | null;
  yearsOfExperience?: number | null;
  roles?: Array<{ roleId: string; roleName: string; scopeType?: string; scopeId?: string }>;
  isEmailVerified?: boolean;
}

export interface RoleModel {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystem: boolean;
  permissionCodes: string[];
  scope?: string;
}

export interface PermissionModel {
  id: string;
  code: string;
  name: string;
  category: 'Identity' | 'Planning' | 'Delivery' | 'System';
  module: string;
  description: string;
}

export interface SkillModel {
  id: string;
  name: string;
  category: string;
  description: string;
}

export interface UserSkillModel {
  id: string;
  userId: string;
  skillId: string;
  skillName: string;
  proficiencyLevel: number;
}

export interface NotificationModel {
  id: string;
  userId: string;
  title: string;
  message: string;
  type: 'SECURITY' | 'AI_SUGGESTION' | 'TASK' | 'SYSTEM';
  category: 'System' | 'Assignment' | 'Mention' | 'Security';
  isRead: boolean;
  createdAt: string;
  actionUrl?: string;
}

export interface AiModelConfig {
  id: string;
  name: string;
  modelName: string;
  provider: 'OpenAI' | 'Anthropic' | 'DeepSeek' | 'Self-Hosted';
  maxTokens: number;
  temperature: number;
  isActive: boolean;
  isEnabled: boolean;
  isDefault: boolean;
  monthlyTokenQuota: number;
  currentMonthTokensUsed?: number;
}

export interface AiGenLogModel {
  id: string;
  userDisplayName: string;
  modelName: string;
  promptSnippet: string;
  tokensUsed: number;
  latencyMs: number;
  costEstimate: number;
  timestamp: string;
  status: 'Success' | 'Filtered' | 'Error';
}

export interface ActiveSessionModel {
  id: string;
  device: string;
  deviceName: string;
  browser: string;
  ipAddress: string;
  location: string;
  lastActive: string;
  isCurrent: boolean;
}
