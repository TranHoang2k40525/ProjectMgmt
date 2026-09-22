export type MotionProfileName =
  | 'auth'
  | 'workspace'
  | 'planning'
  | 'kanban'
  | 'blueprint'
  | 'data'
  | 'scanner'
  | 'ai'
  | 'precision';

export interface MotionProfile {
  readonly name: MotionProfileName;
  readonly enterX: number;
  readonly enterY: number;
  readonly enterScale: number;
  readonly stagger: number;
  readonly duration: number;
  readonly ease: string;
}

const PROFILES: Record<MotionProfileName, MotionProfile> = {
  auth: { name: 'auth', enterX: 0, enterY: 18, enterScale: 0.985, stagger: 0.055, duration: 0.62, ease: 'power3.out' },
  workspace: { name: 'workspace', enterX: 0, enterY: 20, enterScale: 0.99, stagger: 0.045, duration: 0.52, ease: 'power3.out' },
  planning: { name: 'planning', enterX: -18, enterY: 8, enterScale: 1, stagger: 0.04, duration: 0.5, ease: 'power2.out' },
  kanban: { name: 'kanban', enterX: 24, enterY: 0, enterScale: 0.99, stagger: 0.055, duration: 0.56, ease: 'power3.out' },
  blueprint: { name: 'blueprint', enterX: 0, enterY: 14, enterScale: 0.985, stagger: 0.05, duration: 0.58, ease: 'expo.out' },
  data: { name: 'data', enterX: 0, enterY: 24, enterScale: 0.97, stagger: 0.06, duration: 0.62, ease: 'power3.out' },
  scanner: { name: 'scanner', enterX: 0, enterY: 12, enterScale: 0.99, stagger: 0.04, duration: 0.48, ease: 'power2.out' },
  ai: { name: 'ai', enterX: 0, enterY: 18, enterScale: 0.96, stagger: 0.07, duration: 0.66, ease: 'back.out(1.12)' },
  precision: { name: 'precision', enterX: 12, enterY: 0, enterScale: 1, stagger: 0.035, duration: 0.42, ease: 'power2.out' }
};

export function resolveMotionProfile(url: string): MotionProfile {
  if (url.startsWith('/auth')) return PROFILES.auth;
  if (url.includes('/admin/ai-governance') || url.includes('/ai-dataops')) return PROFILES.ai;
  if (url.includes('/admin/identity') || url.includes('/profile') || url.includes('/members')) return PROFILES.scanner;
  if (url.includes('/reports')) return PROFILES.data;
  if (url.includes('/roadmap')) return PROFILES.blueprint;
  if (url.includes('/board')) return PROFILES.kanban;
  if (url.includes('/backlog') || url.includes('/task-list')) return PROFILES.planning;
  if (url.includes('/settings') || url.includes('/projects/new')) return PROFILES.precision;
  return PROFILES.workspace;
}

