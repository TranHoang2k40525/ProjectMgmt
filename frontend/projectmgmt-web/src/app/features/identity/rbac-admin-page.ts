import { forkJoin, from, concatMap, finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/http/api-error-message';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { IdentityService } from '../../core/services/identity.service';
import { UserProfileModel, RoleModel, PermissionModel } from '../../core/mocks/identity-mock-db';

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

  readonly users = signal<UserProfileModel[]>([]);
  readonly roles = signal<RoleModel[]>([]);
  readonly permissions = signal<PermissionModel[]>([]);
  readonly activeView = signal<'MATRIX' | 'USERS'>('MATRIX');
  readonly loading = signal<boolean>(false);
  readonly errorMsg = signal<string | null>(null);
  private readonly savedPermissions = new Map<string, string>();
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
      name: ['', [Validators.required, Validators.pattern(/^[A-Za-z][A-Za-z0-9_-]{1,79}$/)]],
      description: ['', [Validators.maxLength(255)]]
    });

    this.loadAllData();
  }

  loadAllData(): void {
    this.loading.set(true);
    this.errorMsg.set(null);
    forkJoin({ users: this.identityService.getUsers(), roles: this.identityService.getRoles(), permissions: this.identityService.getPermissions() })
      .pipe(finalize(() => this.loading.set(false))).subscribe({
        next: data => {
          this.users.set(data.users); this.roles.set(data.roles); this.permissions.set(data.permissions);
          this.savedPermissions.clear();
          data.roles.forEach(r => this.savedPermissions.set(r.id, this.permissionKey(r)));
        },
        error: err => this.errorMsg.set(apiErrorMessage(err, 'Không thể tải dữ liệu quản trị. Bạn cần quyền quản trị phân quyền hệ thống.'))
      });
  }

  private permissionKey(role: RoleModel): string {
    return [...role.permissionCodes].sort().join('|');
  }

  // --- PERMISSION MATRIX INTERACTION ---
  hasPermission(role: RoleModel, permCode: string): boolean {
    return role.permissionCodes.includes(permCode);
  }

  togglePermission(role: RoleModel, permCode: string): void {
    if (this.loading()) return;
    const set = new Set(role.permissionCodes);
    if (set.has(permCode)) {
      set.delete(permCode);
    } else {
      set.add(permCode);
    }
    role.permissionCodes = Array.from(set);
  }

  saveMatrix(): void {
    if (this.loading()) return;
    const changed = this.roles().filter(r => this.savedPermissions.get(r.id) !== this.permissionKey(r));
    this.successMsg.set(null); this.errorMsg.set(null);
    if (!changed.length) { this.successMsg.set('Không có thay đổi cần lưu.'); return; }
    this.loading.set(true);
    from(changed).pipe(
      concatMap(role => this.identityService.updateRolePermissions(role.id, role.permissionCodes)),
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: role => this.savedPermissions.set(role.id, this.permissionKey(role)),
      complete: () => this.successMsg.set('Đã lưu ma trận phân quyền.'),
      error: err => this.errorMsg.set(apiErrorMessage(err, 'Chưa lưu được hết thay đổi. Các ô chưa lưu được giữ lại để thử lại.'))
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
    if (this.roleForm.invalid || this.loading()) return;
    const { name, description } = this.roleForm.value;
    this.loading.set(true); this.errorMsg.set(null); this.successMsg.set(null);
    this.identityService.createRole(name, description, Array.from(this.newRolePermissions))
      .pipe(finalize(() => this.loading.set(false))).subscribe({
        next: role => {
          this.roles.update(roles => [...roles, role]);
          this.savedPermissions.set(role.id, this.permissionKey(role));
          this.closeRoleModal();
          this.successMsg.set('Đã tạo vai trò cùng các quyền đã chọn.');
        },
        error: err => this.errorMsg.set(apiErrorMessage(err, 'Không thể tạo vai trò.'))
      });
  }

  // Filtered Users
  get filteredUsers(): UserProfileModel[] {
    return this.users().filter(u => {
      const matchSearch = u.displayName.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
                          u.email.toLowerCase().includes(this.searchQuery.toLowerCase());
      const matchRole = !this.selectedRoleFilter || (u.roles?.some(r => r.roleId === this.selectedRoleFilter) ?? u.roleId === this.selectedRoleFilter);
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
