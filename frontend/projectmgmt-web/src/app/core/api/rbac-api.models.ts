import { ApiResult } from './account-api.models';

export interface RoleDto extends ApiResult {
  roleId?: string | null;
  name?: string | null;
  scope?: string | null;
  isSystem?: boolean | null;
  description?: string | null;
  permissionId?: string | null;
  code?: string | null;
  grouping?: string | null;
  permissionIds?: string[] | null;
  items?: RoleDto[] | null;
  roles?: RoleDto[] | null;
  permissions?: RoleDto[] | null;
  members?: RoleDto[] | null;
  userId?: string | null;
  userRoleId?: string | null;
  roleName?: string | null;
  scopeType?: string | null;
  scopeId?: string | null;
  grantedBy?: string | null;
  grantedAt?: string | null;
  updatedPermissionCount?: number | null;
  fullName?: string | null;
  email?: string | null;
  avatarUrl?: string | null;
  newRoleId?: string | null;
  newRoleName?: string | null;
  removed?: boolean | null;
}
