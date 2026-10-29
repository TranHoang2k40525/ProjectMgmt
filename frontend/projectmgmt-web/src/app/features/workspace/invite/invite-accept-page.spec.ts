import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ToastService } from '../../../core/services/toast.service';
import { WorkspaceDataSource } from '../data/workspace-data-source';
import { WorkspaceContextService } from '../workspace-context.service';
import { InviteAcceptPageComponent } from './invite-accept-page';

describe('InviteAcceptPageComponent', () => {
  const data = { getInviteByToken: vi.fn(), acceptInviteLink: vi.fn() };
  const navigate = vi.fn(() => Promise.resolve(true));
  const upsert = vi.fn();

  beforeEach(() => {
    data.getInviteByToken.mockReset();
    data.acceptInviteLink.mockReset();
    navigate.mockClear();
    TestBed.configureTestingModule({
      providers: [
        { provide: WorkspaceDataSource, useValue: data },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ token: 'tok' }) } } },
        { provide: Router, useValue: { navigate } },
        { provide: WorkspaceContextService, useValue: { upsert } },
        { provide: ToastService, useValue: { success: vi.fn() } }
      ]
    });
  });

  function create(): InviteAcceptPageComponent {
    const page = TestBed.runInInjectionContext(() => new InviteAcceptPageComponent());
    page.ngOnInit();
    return page;
  }

  it('joins the organization from the link and opens it', () => {
    const preview = { token: 'tok', orgId: 'org', orgName: 'Lab', invitedByName: 'Hà', role: 'Member', alreadyMember: false };
    data.getInviteByToken.mockReturnValue(of(preview));
    data.acceptInviteLink.mockReturnValue(of({ id: 'org', name: 'Lab' }));
    const page = create();
    page.join(page.preview()!);
    expect(data.acceptInviteLink).toHaveBeenCalledWith('tok');
    expect(upsert).toHaveBeenCalledWith({ id: 'org', name: 'Lab' });
    expect(navigate).toHaveBeenCalledWith(['/orgs', 'org', 'overview']);
  });

  it('explains a revoked link instead of repeating the server title', () => {
    data.getInviteByToken.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404, error: { title: 'Link mời không còn hiệu lực.' } })));
    const page = create();
    expect(page.preview()).toBeNull();
    expect(page.loadError()).toContain('xin người mời gửi link mới');
  });
});
