import { AfterViewInit, Directive, ElementRef, Input, OnDestroy, inject } from '@angular/core';
import { gsap } from 'gsap';
import { MotionOrchestratorService } from '../../core/motion/motion-orchestrator.service';

@Directive({
  selector: '[appMotion]',
  standalone: true
})
export class MotionDirective implements AfterViewInit, OnDestroy {
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly motion = inject(MotionOrchestratorService);
  private tween: gsap.core.Tween | null = null;

  @Input('appMotion') preset: 'rise' | 'scale' | 'slide' = 'rise';

  ngAfterViewInit(): void {
    this.tween = this.motion.animateElement(this.elementRef.nativeElement, this.preset);
  }

  ngOnDestroy(): void {
    this.tween?.kill();
  }
}

