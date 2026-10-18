import { describe, expect, it } from 'vitest';
import { resolveMotionProfile } from './motion-profile';

describe('resolveMotionProfile', () => {
  it('maps the main product route families to distinct motion profiles', () => {
    expect(resolveMotionProfile('/auth').name).toBe('auth');
    expect(resolveMotionProfile('/backlog').name).toBe('planning');
    expect(resolveMotionProfile('/board').name).toBe('kanban');
    expect(resolveMotionProfile('/roadmap').name).toBe('blueprint');
    expect(resolveMotionProfile('/reports').name).toBe('data');
    expect(resolveMotionProfile('/profile').name).toBe('scanner');
    expect(resolveMotionProfile('/admin/ai-governance').name).toBe('ai');
    expect(resolveMotionProfile('/project/settings').name).toBe('precision');
  });

  it('uses workspace motion as the safe default', () => {
    expect(resolveMotionProfile('/for-you').name).toBe('workspace');
    expect(resolveMotionProfile('/unknown').name).toBe('workspace');
  });
});
