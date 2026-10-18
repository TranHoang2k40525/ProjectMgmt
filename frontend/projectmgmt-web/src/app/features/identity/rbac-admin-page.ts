import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { IdentityService } from '../../core/services/identity.service';
import { UserProfileModel, RoleModel, PermissionModel, IdentityMockDb } from '../../core/mocks/identity-mock-db';

@Component({
  selector: 'app-rbac-admin-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './rbac-admin-page.html',
  styleUrls: ['./rbac-admin-page.scss']
})
export class RbacAdminPageComponent implements OnInit {
  private identityService = inject(IdentityService);
  private fb = inject(FormBuilder);

  readonly users = signal<UserProfileModel[]>(IdentityMockDb.users);
  readonly roles = signal<RoleModel[]>(IdentityMockDb.roles);
  readonly permissions = signal<PermissionModel[]>(IdentityMockDb.permissions);
  readonly activeView = signal<'MATRIX' | 'USERS'>('MATRIX');
  readonly loading = signal<boolean>(false);
  readonly successMsg = signal<string | null>(null);

  // Search and Filters
  searchQuery: string = '';
  selectedRoleFilter: string = '';

  // New Role Modal state
  showRoleModal: boolean = false;
  roleForm!: FormGroup;

  // Selected permissions for creating role
  newRolePermissions: Set<string> = new Set();

  ngOnInit(): void {
    this.roleForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(3)]],
      description: ['', [Validators.required]]
    });

    this.loadAllData();
  }

  loadAllData(): void {
    this.identityService.getUsers().subscribe(res => this.users.set(res));
    this.identityService.getRoles().subscribe(res => this.roles.set(res));
    this.identityService.getPermissions().subscribe(res => this.permissions.set(res));
  }

  // --- PERMISSION MATRIX INTERACTION ---
  hasPermission(role: RoleModel, permCode: string): boolean {
    return role.permissionCodes.includes(permCode);
  }

  togglePermission(role: RoleModel, permCode: string): void {
    const set = new Set(role.permissionCodes);
    if (set.has(permCode)) {
      set.delete(permCode);
    } else {
      set.add(permCode);
    }
    role.permissionCodes = Array.from(set);
  }

  saveMatrix(): void {
    this.loading.set(true);
    let pending = this.roles().length;

    this.roles().forEach(r => {
      this.identityService.updateRolePermissions(r.id, r.permissionCodes).subscribe({
        next: () => {
          pending--;
          if (pending === 0) {
            this.loading.set(false);
            this.successMsg.set('Ma trận phân quyền hệ thống đã được lưu thành công!');
          }
        }
      });
    });
  }

  // --- USER ROLE & STATUS TOGGLES ---
  onUserRoleChange(userId: string, event: Event): void {
    const roleId = (event.target as HTMLSelectElement).value;
    this.identityService.updateUserRole(userId, roleId).subscribe(() => {
      this.loadAllData();
      this.successMsg.set('Đã cập nhật vai trò người dùng.');
    });
  }

  onToggleUserStatus(userId: string): void {
    this.identityService.toggleUserActive(userId).subscribe(() => {
      this.loadAllData();
      this.successMsg.set('Đã thay đổi trạng thái kích hoạt tài khoản.');
    });
  }

  // --- CREATE ROLE MODAL ---
  openRoleModal(): void {
    this.roleForm.reset();
    this.newRolePermissions.clear();
    this.showRoleModal = true;
  }

  closeRoleModal(): void {
    this.showRoleModal = false;
  }

  toggleNewRolePerm(code: string): void {
    if (this.newRolePermissions.has(code)) {
      this.newRolePermissions.delete(code);
    } else {
      this.newRolePermissions.add(code);
    }
  }

  submitNewRole(): void {
    if (this.roleForm.invalid) return;

    const { name, description } = this.roleForm.value;
    const perms = Array.from(this.newRolePermissions);

    this.identityService.createRole(name, description, perms).subscribe(() => {
      this.loadAllData();
      this.closeRoleModal();
      this.successMsg.set(`Vai trò mới "${name}" đã được tạo thành công!`);
    });
  }

  // Filtered Users
  get filteredUsers(): UserProfileModel[] {
    return this.users().filter(u => {
      const matchSearch = u.displayName.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
                          u.email.toLowerCase().includes(this.searchQuery.toLowerCase());
      const matchRole = !this.selectedRoleFilter || u.roleId === this.selectedRoleFilter;
      return matchSearch && matchRole;
    });
  }

  // Group Permissions by Module
  get groupedPermissions(): { module: string; items: PermissionModel[] }[] {
    const map = new Map<string, PermissionModel[]>();
    for (const p of this.permissions()) {
      const list = map.get(p.module) || [];
      list.push(p);
      map.set(p.module, list);
    }
    return Array.from(map.entries()).map(([module, items]) => ({ module, items }));
  }
}
