import { describe, beforeEach, it, expect } from 'vitest';
import { ProfilePageComponent } from './profile-page';
import { IdentityService } from '../../core/services/identity.service';
import { FormBuilder } from '@angular/forms';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';

describe('ProfilePageComponent (Unit Tests)', () => {
  let component: ProfilePageComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [IdentityService, FormBuilder]
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
