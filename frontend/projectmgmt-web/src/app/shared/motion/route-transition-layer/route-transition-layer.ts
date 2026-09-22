import { AfterViewInit, ChangeDetectionStrategy, Component, DestroyRef, ElementRef, OnDestroy, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs/operators';
import { gsap } from 'gsap';
import { resolveMotionProfile } from '../../../core/motion/motion-profile';
import { MotionPreferencesService } from '../../../core/motion/motion-preferences.service';

@Component({
  selector: 'app-route-transition-layer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="route-transition" [attr.data-profile]="profile()" aria-hidden="true">
      <div class="route-transition__wash"></div>
      <div class="route-transition__grid"></div>
      <div class="route-transition__beam"></div>
      <div class="route-transition__orbit route-transition__orbit--one"></div>
      <div class="route-transition__orbit route-transition__orbit--two"></div>
      <div class="route-transition__nodes">
        <i></i><i></i><i></i><i></i><i></i>
      </div>
    </div>
  `,
  styles: [`
    :host { position: fixed; inset: 0; z-index: 55; pointer-events: none; overflow: hidden; }
    .route-transition { position: absolute; inset: 0; visibility: hidden; opacity: 0; overflow: hidden; }
    .route-transition__wash { position: absolute; inset: 0; background: linear-gradient(110deg, transparent 5%, rgb(37 99 235 / .08) 38%, rgb(14 165 233 / .16) 50%, transparent 72%); }
    .route-transition__grid { position: absolute; inset: 0; opacity: .2; background-image: linear-gradient(rgb(37 99 235 / .22) 1px, transparent 1px), linear-gradient(90deg, rgb(37 99 235 / .22) 1px, transparent 1px); background-size: 32px 32px; mask-image: linear-gradient(90deg, transparent, #000 28%, #000 72%, transparent); }
    .route-transition__beam { position: absolute; left: 8%; right: 8%; top: 50%; height: 2px; transform-origin: left center; background: linear-gradient(90deg, transparent, #2563eb, #22d3ee, transparent); box-shadow: 0 0 18px rgb(37 99 235 / .35); }
    .route-transition__orbit { position: absolute; left: 50%; top: 50%; width: min(54vw, 680px); aspect-ratio: 1; border: 1px solid rgb(37 99 235 / .22); border-radius: 50%; }
    .route-transition__orbit--two { width: min(34vw, 430px); border-style: dashed; }
    .route-transition__nodes { position: absolute; inset: 0; }
    .route-transition__nodes i { position: absolute; width: 8px; height: 8px; border-radius: 50%; background: #2563eb; box-shadow: 0 0 0 6px rgb(37 99 235 / .1); }
    .route-transition__nodes i:nth-child(1) { left: 18%; top: 28%; }
    .route-transition__nodes i:nth-child(2) { left: 35%; top: 62%; }
    .route-transition__nodes i:nth-child(3) { left: 52%; top: 34%; }
    .route-transition__nodes i:nth-child(4) { left: 70%; top: 68%; }
    .route-transition__nodes i:nth-child(5) { left: 84%; top: 42%; }
    @media (max-width: 767px) {
      .route-transition__orbit { width: 92vw; }
      .route-transition__orbit--two { width: 64vw; }
      .route-transition__grid { background-size: 24px 24px; }
    }
    @media (prefers-reduced-motion: reduce) { :host { display: none; } }
  `]
})
export class RouteTransitionLayerComponent implements AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly preferences = inject(MotionPreferencesService);
  private timeline: gsap.core.Timeline | null = null;

  readonly profile = signal(resolveMotionProfile(this.router.url).name);

  ngAfterViewInit(): void {
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(event => this.play(event.urlAfterRedirects || event.url));
  }

  ngOnDestroy(): void {
    this.timeline?.kill();
  }

  private play(url: string): void {
    if (this.preferences.reduceMotion()) return;

    const root = this.elementRef.nativeElement.querySelector<HTMLElement>('.route-transition');
    if (!root) return;

    const profile = resolveMotionProfile(url);
    this.profile.set(profile.name);
    this.timeline?.kill();

    const wash = root.querySelector('.route-transition__wash');
    const grid = root.querySelector('.route-transition__grid');
    const beam = root.querySelector('.route-transition__beam');
    const orbits = root.querySelectorAll('.route-transition__orbit');
    const nodes = root.querySelectorAll('.route-transition__nodes i');

    const timeline = gsap.timeline({
      defaults: { overwrite: 'auto' },
      onComplete: () => gsap.set(root, { autoAlpha: 0 })
    });
    this.timeline = timeline;
    timeline.set(root, { autoAlpha: 1 });

    if (profile.name === 'kanban' || profile.name === 'planning') {
      timeline
        .fromTo(wash, { xPercent: profile.name === 'kanban' ? 90 : -90 }, { xPercent: 0, duration: .28, ease: 'power3.out' })
        .to(wash, { xPercent: profile.name === 'kanban' ? -80 : 80, autoAlpha: 0, duration: .34, ease: 'power2.in' });
    } else if (profile.name === 'data' || profile.name === 'blueprint') {
      timeline
        .fromTo(grid, { autoAlpha: 0, scale: 1.04 }, { autoAlpha: .52, scale: 1, duration: .24 })
        .fromTo(beam, { scaleX: 0 }, { scaleX: 1, duration: .42, ease: 'expo.out' }, '<')
        .to([grid, beam], { autoAlpha: 0, duration: .24 });
    } else if (profile.name === 'scanner') {
      timeline
        .fromTo(beam, { y: '-38vh', scaleX: .25 }, { y: '38vh', scaleX: 1, duration: .48, ease: 'power2.inOut' })
        .to(beam, { autoAlpha: 0, duration: .16 });
    } else if (profile.name === 'ai' || profile.name === 'auth') {
      timeline
        .fromTo(orbits, { autoAlpha: 0, scale: .72, rotation: -35 }, { autoAlpha: .62, scale: 1, rotation: 12, duration: .46, stagger: .05, ease: 'back.out(1.35)' })
        .fromTo(nodes, { autoAlpha: 0, scale: 0 }, { autoAlpha: 1, scale: 1, duration: .22, stagger: .035 }, '<.08')
        .to([orbits, nodes], { autoAlpha: 0, scale: 1.08, duration: .22 });
    } else {
      timeline
        .fromTo(wash, { autoAlpha: 0, scale: 1.04 }, { autoAlpha: 1, scale: 1, duration: .24 })
        .to(wash, { autoAlpha: 0, duration: .28 });
    }
  }
}
