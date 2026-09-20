import { Injectable } from '@angular/core';
import { gsap } from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';
import { resolveMotionProfile } from './motion-profile';

gsap.registerPlugin(ScrollTrigger);

@Injectable({ providedIn: 'root' })
export class MotionOrchestratorService {
  private activeMedia: gsap.MatchMedia | null = null;

  animatePage(host: HTMLElement, url: string): void {
    this.activeMedia?.revert();
    this.activeMedia = null;

    const page = host.querySelector<HTMLElement>(':scope > router-outlet + *');
    if (!page) return;

    const profile = resolveMotionProfile(url);
    const media = gsap.matchMedia(page);
    this.activeMedia = media;

    media.add(
      {
        animate: '(prefers-reduced-motion: no-preference)',
        desktop: '(min-width: 1280px)',
        compact: '(max-width: 1279px)'
      },
      context => {
        const { animate, compact } = context.conditions as { animate: boolean; desktop: boolean; compact: boolean };
        if (!animate) return;

        const heading = page.querySelector<HTMLElement>('h1, h2');
        const candidates = gsap.utils.toArray<HTMLElement>(
          '.card, .project-card, .workspace-card, .summary-card, .stat-card, .kpi-card, .model-card, .notification-item, .sprint-container, .board-column, .task-row, .work-item, .logs-card, form, table',
          page
        ).slice(0, compact ? 10 : 16);

        const timeline = gsap.timeline({ defaults: { overwrite: 'auto' } });
        if (heading) {
          timeline.fromTo(
            heading,
            { autoAlpha: 0, x: profile.enterX * 0.6, y: profile.enterY, scale: profile.enterScale },
            { autoAlpha: 1, x: 0, y: 0, scale: 1, duration: profile.duration, ease: profile.ease, clearProps: 'transform,opacity,visibility' }
          );
        }

        const inView: HTMLElement[] = [];
        const belowFold: HTMLElement[] = [];
        candidates.forEach(element => {
          const rect = element.getBoundingClientRect();
          (rect.top < window.innerHeight * 0.94 ? inView : belowFold).push(element);
        });

        if (inView.length) {
          timeline.fromTo(
            inView,
            { autoAlpha: 0, x: profile.enterX, y: profile.enterY, scale: profile.enterScale },
            {
              autoAlpha: 1,
              x: 0,
              y: 0,
              scale: 1,
              duration: compact ? profile.duration * 0.82 : profile.duration,
              stagger: compact ? Math.min(profile.stagger, 0.035) : profile.stagger,
              ease: profile.ease,
              clearProps: 'transform,opacity,visibility'
            },
            heading ? '<0.12' : 0
          );
        }

        belowFold.forEach((element, index) => {
          gsap.fromTo(
            element,
            { autoAlpha: 0, y: Math.min(profile.enterY + 8, 28), scale: Math.max(profile.enterScale, 0.985) },
            {
              autoAlpha: 1,
              y: 0,
              scale: 1,
              duration: profile.duration,
              delay: (index % 3) * 0.035,
              ease: profile.ease,
              clearProps: 'transform,opacity,visibility',
              scrollTrigger: {
                trigger: element,
                start: 'top 92%',
                once: true
              }
            }
          );
        });
      }
    );
  }

  animateElement(element: HTMLElement, preset: 'rise' | 'scale' | 'slide' = 'rise'): gsap.core.Tween | null {
    if (typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches) return null;

    const from = preset === 'scale'
      ? { autoAlpha: 0, scale: 0.94 }
      : preset === 'slide'
        ? { autoAlpha: 0, x: 18 }
        : { autoAlpha: 0, y: 16 };

    return gsap.fromTo(element, from, {
      autoAlpha: 1,
      x: 0,
      y: 0,
      scale: 1,
      duration: 0.46,
      ease: 'power3.out',
      clearProps: 'transform,opacity,visibility'
    });
  }

  destroy(): void {
    this.activeMedia?.revert();
    this.activeMedia = null;
  }
}
