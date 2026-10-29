import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { WorkspaceDataSource } from '../data/workspace-data-source';
import { CreateOrgModalComponent } from './create-org-modal';

describe('CreateOrgModalComponent', () => {
  const createOrganization = vi.fn();

  beforeEach(() => {
    createOrganization.mockReset();
    TestBed.configureTestingModule({ providers: [{ provide: WorkspaceDataSource, useValue: { createOrganization } }] });
  });

  function create(): CreateOrgModalComponent {
    return TestBed.runInInjectionContext(() => new CreateOrgModalComponent());
  }

  it('suggests the slug from the name until the user edits it', () => {
    const modal = create();
    modal.onNameChange('Phòng Đào tạo');
    expect(modal.slug()).toBe('phong-dao-tao');
    modal.onSlugChange('dao-tao');
    modal.onNameChange('Phòng Đào tạo HUCE');
    expect(modal.slug()).toBe('dao-tao');
  });

  it('validates locally before calling the data source', () => {
    const modal = create();
    modal.onNameChange('A');
    modal.submit();
    expect(modal.nameError()).not.toBe('');
    expect(createOrganization).not.toHaveBeenCalled();
  });

  it('emits the created organization', () => {
    const org = { id: 'org-1', name: 'Phòng Đào tạo' };
    createOrganization.mockReturnValue(of(org));
    const modal = create();
    const created = vi.fn();
    modal.created.subscribe(created);
    modal.onNameChange('Phòng Đào tạo');
    modal.submit();
    expect(createOrganization).toHaveBeenCalledWith({ name: 'Phòng Đào tạo', slug: 'phong-dao-tao' });
    expect(created).toHaveBeenCalledWith(org);
  });

  it('shows a duplicate slug error on the slug field', () => {
    createOrganization.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { title: 'Đường dẫn tổ chức đã được dùng.', code: 'ORG_SLUG_DUPLICATE' }
    })));
    const modal = create();
    modal.onNameChange('Phòng Đào tạo');
    modal.submit();
    expect(modal.slugError()).toBe('Đường dẫn tổ chức đã được dùng.');
    expect(modal.saving()).toBe(false);
  });
});
