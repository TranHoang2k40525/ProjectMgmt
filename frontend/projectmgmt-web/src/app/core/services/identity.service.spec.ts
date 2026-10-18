import { TestBed } from '@angular/core/testing';
import { describe, beforeEach, it, expect } from 'vitest';
import { firstValueFrom } from 'rxjs';
import { IdentityService } from './identity.service';
import { IdentityMockDb } from '../mocks/identity-mock-db';

describe('IdentityService (Mock Unit Tests)', () => {
  let service: IdentityService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(IdentityService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should initialize with active mock user state', () => {
    const state = service.authState();
    expect(state.isAuthenticated).toBe(true);
    expect(state.currentUser).not.toBeNull();
  });

  it('should handle user login successfully for valid user', async () => {
    const res = await firstValueFrom(service.login('admin@scrumai.internal', 'password123'));
    expect(res.user.email).toBe('admin@scrumai.internal');
    expect(res.token).toBeDefined();
  });

  it('should reject login for non-existent user', async () => {
    try {
      await firstValueFrom(service.login('nonexistent@test.com', 'pass'));
    } catch (err: unknown) {
      const errorResponse = err as { error: { title: string } };
      expect(errorResponse.error.title).toContain('không tồn tại');
    }
  });

  it('should send and verify OTP correctly', async () => {
    const email = 'newuser@scrumai.internal';
    const sendRes = await firstValueFrom(service.sendOtp(email, 'REGISTER'));
    expect(sendRes.success).toBe(true);

    const verifyRes = await firstValueFrom(service.verifyOtp(email, '123456', 'REGISTER'));
    expect(verifyRes.success).toBe(true);
    expect(service.authState().currentUser?.email).toBe(email);
  });

  it('should update profile correctly', async () => {
    const userId = IdentityMockDb.users[0].id;
    const updated = await firstValueFrom(service.updateProfile(userId, { jobTitle: 'Principal Lead Architect' }));
    expect(updated.jobTitle).toBe('Principal Lead Architect');
    expect(service.authState().currentUser?.jobTitle).toBe('Principal Lead Architect');
  });

  it('should add and remove user skills', async () => {
    const userId = IdentityMockDb.users[0].id;
    const added = await firstValueFrom(service.addUserSkill(userId, 'sk-1', 95));
    expect(added.proficiencyLevel).toBe(95);

    const res = await firstValueFrom(service.removeUserSkill(added.id));
    expect(res.success).toBe(true);
  });

  it('should toggle user active status', async () => {
    const user = IdentityMockDb.users[1];
    const initialStatus = user.isActive;
    const updated = await firstValueFrom(service.toggleUserActive(user.id));
    expect(updated.isActive).toBe(!initialStatus);
  });
});
