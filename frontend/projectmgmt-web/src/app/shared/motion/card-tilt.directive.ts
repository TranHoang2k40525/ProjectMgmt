import { Directive, ElementRef, HostListener, Input, OnDestroy, OnInit, inject } from '@angular/core';
import { gsap } from 'gsap';

/**
 * Animate UI + GSAP 3D Interactive Card Tilt Directive
 * Theo chuẩn gsap-performance: Sử dụng gsap.quickTo() để tối ưu 60fps trên compositor layer,
 * không gây layout thrashing (chỉ transform rotationX/Y và scale), tự động dọn dẹp khi unmount.
 */
@Directive({
  selector: '[appCardTilt]',
  standalone: true
})
export class CardTiltDirective implements OnInit, OnDestroy {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);

  @Input() maxTilt = 5; // Độ nghiêng tối đa (độ) - tinh tế, chuyên nghiệp
  @Input() scaleOnHover = 1.015; // Phóng to nhẹ khi di chuột
  @Input() perspective = 1000;

  private rotateXTo: ((value: number) => void) | null = null;
  private rotateYTo: ((value: number) => void) | null = null;
  private scaleTo: ((value: number) => void) | null = null;
  private isReducedMotion = false;

  ngOnInit(): void {
    if (typeof window !== 'undefined' && window.matchMedia) {
      this.isReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }
    if (this.isReducedMotion) return;

    const nativeEl = this.el.nativeElement;
    nativeEl.style.transformStyle = 'preserve-3d';
    nativeEl.style.willChange = 'transform';

    // Tạo gsap.quickTo để tái sử dụng tween, tránh rác bộ nhớ và giật khung hình
    this.rotateXTo = gsap.quickTo(nativeEl, 'rotationX', { duration: 0.35, ease: 'power2.out' });
    this.rotateYTo = gsap.quickTo(nativeEl, 'rotationY', { duration: 0.35, ease: 'power2.out' });
    this.scaleTo = gsap.quickTo(nativeEl, 'scale', { duration: 0.35, ease: 'power2.out' });
  }

  @HostListener('mouseenter')
  onMouseEnter(): void {
    if (this.isReducedMotion) return;
    this.scaleTo?.(this.scaleOnHover);
  }

  @HostListener('mousemove', ['$event'])
  onMouseMove(e: MouseEvent): void {
    if (this.isReducedMotion) return;

    const rect = this.el.nativeElement.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    const centerX = rect.width / 2;
    const centerY = rect.height / 2;

    const percentX = (x - centerX) / centerX;
    const percentY = (y - centerY) / centerY;

    // rotationX nghiêng theo trục Y ngược chiều, rotationY nghiêng theo trục X thuận chiều
    const tiltX = -percentY * this.maxTilt;
    const tiltY = percentX * this.maxTilt;

    this.rotateXTo?.(tiltX);
    this.rotateYTo?.(tiltY);
  }

  @HostListener('mouseleave')
  onMouseLeave(): void {
    if (this.isReducedMotion) return;
    this.rotateXTo?.(0);
    this.rotateYTo?.(0);
    this.scaleTo?.(1);
  }

  ngOnDestroy(): void {
    gsap.killTweensOf(this.el.nativeElement);
  }
}

