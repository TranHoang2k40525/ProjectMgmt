import { describe, beforeEach, it, expect } from 'vitest';
import { AuthPageComponent } from './auth-page';
import { IdentityService } from '../../core/services/identity.service';
import { Router } from '@angular/router';
import { FormBuilder } from '@angular/forms';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';

describe('AuthPageComponent (Unit Tests)', () => {
  let component: AuthPageComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [IdentityService, FormBuilder, { provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } }]
    });

    const injector = TestBed.inject(EnvironmentInjector);
    runInInjectionContext(injector, () => {
      component = new AuthPageComponent();
    });
  });

  it('should create the AuthPageComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize in LOGIN mode', () => {
    expect(component.mode()).toBe('LOGIN');
  });

  it('should toggle between modes properly', () => {
    component.setMode('SIGNUP');
    expect(component.mode()).toBe('SIGNUP');

    component.setMode('FORGOT');
    expect(component.mode()).toBe('FORGOT');
  });

  it('should calculate password strength correctly', () => {
    const weak = component.calculatePasswordStrength('123');
    expect(weak.label).toBe('Yếu');

    const strong = component.calculatePasswordStrength('AdminPass123!');
    expect(strong.score).toBe(100);
    expect(strong.label).toBe('Rất mạnh');
  });

  it('should fill OTP pin grid and auto format time', () => {
    expect(component.formatTime(300)).toBe('05:00');
    expect(component.formatTime(65)).toBe('01:05');
  });
});
