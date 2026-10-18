import { Injectable, inject } from '@angular/core';
import { gsap } from 'gsap';
import { MotionPreferencesService } from '../motion/motion-preferences.service';

@Injectable({ providedIn: 'root' })
export class AttentionOrchestratorService {
  private readonly preferences = inject(MotionPreferencesService);
  private cleanupCurrent: (() => void) | null = null;

  nudge(root: HTMLElement, symbol: HTMLElement, cta: HTMLElement): void {
    this.stop();
    if (this.preferences.reduceMotion() || this.preferences.saveData() || document.hidden) return;

    let timer: ReturnType<typeof setTimeout> | null = null;
    let timeline: gsap.core.Timeline | null = null;
    const arrow = cta.querySelector<HTMLElement>('.material-symbols-outlined');

    const cleanup = (): void => {
      if (timer) clearTimeout(timer);
      timeline?.kill();
      gsap.set([symbol, cta, ...(arrow ? [arrow] : [])], { clearProps: 'transform' });
      symbol.style.willChange = '';
      cta.style.willChange = '';
      root.removeEventListener('pointermove', onInteraction);
      root.removeEventListener('pointerdown', onInteraction);
      root.removeEventListener('focusin', onInteraction);
      root.removeEventListener('wheel', onInteraction);
      document.removeEventListener('visibilitychange', onVisibility);
      if (this.cleanupCurrent === cleanup) this.cleanupCurrent = null;
    };
    const onInteraction = (): void => this.stop();
    const onVisibility = (): void => { if (document.hidden) this.stop(); };
    this.cleanupCurrent = cleanup;

    root.addEventListener('pointermove', onInteraction, { passive: true });
    root.addEventListener('pointerdown', onInteraction, { passive: true });
    root.addEventListener('focusin', onInteraction);
    root.addEventListener('wheel', onInteraction, { passive: true });
    document.addEventListener('visibilitychange', onVisibility);

    timer = setTimeout(() => {
      if (this.cleanupCurrent !== cleanup) return;
      symbol.style.willChange = 'transform';
      cta.style.willChange = 'transform';
      timeline = gsap.timeline({ repeat: 2, repeatDelay: 2.6, onComplete: cleanup });
      timeline
        .to(symbol, { y: -6, rotation: -7, duration: .28, ease: 'power2.out' }, 0)
        .to(symbol, { y: 0, rotation: 0, duration: .44, ease: 'elastic.out(1, .55)' }, .28)
        .to(cta, { scale: 1.035, duration: .24, ease: 'power2.out' }, .06)
        .to(cta, { scale: 1, duration: .34, ease: 'power2.inOut' }, .3);
      if (arrow) timeline.to(arrow, { x: 5, duration: .22, ease: 'power2.out' }, .1)
        .to(arrow, { x: 0, duration: .36, ease: 'power2.out' }, .32);
    }, 1150);
  }

  stop(): void {
    this.cleanupCurrent?.();
  }
}
