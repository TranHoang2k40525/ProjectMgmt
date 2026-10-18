import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

interface NavigatorConnection {
  readonly saveData?: boolean;
  readonly effectiveType?: string;
}

@Injectable({ providedIn: 'root' })
export class MotionPreferencesService {
  private readonly destroyRef = inject(DestroyRef);

  readonly reduceMotion = signal(false);
  readonly coarsePointer = signal(false);
  readonly documentHidden = signal(false);
  readonly saveData = signal(false);
  readonly effectiveConnection = signal('unknown');

  readonly allowContinuousMotion = computed(
    () => !this.reduceMotion() && !this.documentHidden() && !this.saveData()
  );

  constructor() {
    if (typeof window === 'undefined' || typeof document === 'undefined') return;

    const reducedQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
    const coarseQuery = window.matchMedia('(pointer: coarse)');
    const connection = (navigator as Navigator & { connection?: NavigatorConnection }).connection;

    const syncMedia = (): void => {
      this.reduceMotion.set(reducedQuery.matches);
      this.coarsePointer.set(coarseQuery.matches);
    };
    const syncVisibility = (): void => this.documentHidden.set(document.hidden);

    syncMedia();
    syncVisibility();
    this.saveData.set(Boolean(connection?.saveData));
    this.effectiveConnection.set(connection?.effectiveType ?? 'unknown');

    reducedQuery.addEventListener('change', syncMedia);
    coarseQuery.addEventListener('change', syncMedia);
    document.addEventListener('visibilitychange', syncVisibility);

    this.destroyRef.onDestroy(() => {
      reducedQuery.removeEventListener('change', syncMedia);
      coarseQuery.removeEventListener('change', syncMedia);
      document.removeEventListener('visibilitychange', syncVisibility);
    });
  }

  supportsWebGl(): boolean {
    if (typeof document === 'undefined') return false;
    try {
      const canvas = document.createElement('canvas');
      return Boolean(canvas.getContext('webgl2'));
    } catch {
      return false;
    }
  }
}

