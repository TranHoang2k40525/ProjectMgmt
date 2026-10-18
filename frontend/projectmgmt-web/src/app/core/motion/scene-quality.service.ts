import { Injectable, computed, inject } from '@angular/core';
import { MotionPreferencesService } from './motion-preferences.service';

export type SceneQuality = 'full' | 'balanced' | 'lite' | 'static';

@Injectable({ providedIn: 'root' })
export class SceneQualityService {
  private readonly preferences = inject(MotionPreferencesService);

  readonly quality = computed<SceneQuality>(() => {
    if (!this.preferences.supportsWebGl() || this.preferences.reduceMotion() || this.preferences.saveData()) {
      return 'static';
    }

    if (typeof window === 'undefined') return 'static';

    const width = window.innerWidth;
    const cores = navigator.hardwareConcurrency || 4;
    const memory = (navigator as Navigator & { deviceMemory?: number }).deviceMemory ?? 4;
    const slowConnection = ['slow-2g', '2g'].includes(this.preferences.effectiveConnection());

    if (slowConnection || width < 640 || cores <= 2 || memory <= 2) return 'lite';
    if (width < 1280 || cores <= 4 || memory <= 4) return 'balanced';
    return 'full';
  });
}

