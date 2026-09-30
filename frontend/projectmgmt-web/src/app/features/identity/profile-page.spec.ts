import { describe, beforeEach, it, expect } from 'vitest';
import { ProfilePageComponent } from './profile-page';
import { IdentityService } from '../../core/services/identity.service';
import { FormBuilder } from '@angular/forms';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccountApi } from '../../core/api/account.api';
import { IdentityApi } from '../../core/api/identity.api';
import { TOKEN_STORE } from '../../core/auth/token-store';
import { of } from 'rxjs';
import { Router } from '@angular/router';

describe('ProfilePageComponent (Unit Tests)', () => {
  let component: ProfilePageComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        IdentityService,
        FormBuilder,
        { provide: AccountApi, useValue: {} },
        {
          provide: IdentityApi,
          useValue: {
            getMyProfile: () => of({
              success: true,
              userId: 'user-1',
              email: 'admin@scrumai.internal',
              fullName: 'System Administrator',
              jobTitle: 'System Administrator',
              timezone: 'Asia/Ho_Chi_Minh'
            }),
            getSkillCatalog: () => of({ success: true, items: [] }),
            getUserSkills: () => of({ success: true, skills: [] })
          }
        },
        { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
        {
          provide: TOKEN_STORE,
          useValue: {
            getAccessToken: () => 'access-token',
            getRefreshToken: () => 'refresh-token',
            getSession: () => ({
              accessToken: 'access-token',
              refreshToken: 'refresh-token',
              userId: 'user-1',
              email: 'admin@scrumai.internal',
              fullName: 'System Administrator',
              roles: ['System Administrator']
            }),
            setSession: () => undefined,
            clear: () => undefined
          }
        }
      ]
    });

    const injector = TestBed.inject(EnvironmentInjector);
    runInInjectionContext(injector, () => {
      component = new ProfilePageComponent();
      component.ngOnInit();
    });
  });

  it('should create ProfilePageComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should load default user profile on init', () => {
    expect(component.user()).not.toBeNull();
    expect(component.user()?.email).toBe('admin@scrumai.internal');
  });

  it('should toggle active tabs correctly', () => {
    component.setTab('SECURITY');
    expect(component.activeTab()).toBe('SECURITY');

    component.setTab('SKILLS');
    expect(component.activeTab()).toBe('SKILLS');
  });

  it('should calculate proficiency badge labels', () => {
    expect(component.getProficiencyBadge(30).label).toBe('Cơ bản');
    expect(component.getProficiencyBadge(95).label).toBe('Chuyên gia');
  });
});
