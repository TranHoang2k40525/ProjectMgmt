import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject, NgZone } from '@angular/core';

interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  radius: number;
  alpha: number;
  baseAlpha: number;
}

@Component({
  selector: 'app-dynamic-bg',
  standalone: true,
  template: `
    <div class="dynamic-bg-container">
      <div class="ambient-glow glow-1"></div>
      <div class="ambient-glow glow-2"></div>
      <canvas #canvasElement class="particle-canvas"></canvas>
      <div class="grid-overlay"></div>
    </div>
  `,
  styleUrls: ['./dynamic-bg.scss']
})
export class DynamicBgComponent implements OnInit, OnDestroy {
  @ViewChild('canvasElement', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;

  private ngZone = inject(NgZone);
  private ctx!: CanvasRenderingContext2D | null;
  private particles: Particle[] = [];
  private animationFrameId: number | null = null;
  private mouse = { x: -1000, y: -1000, targetX: -1000, targetY: -1000 };
  private resizeObserver!: ResizeObserver;

  ngOnInit(): void {
    const canvas = this.canvasRef.nativeElement;
    this.ctx = canvas.getContext('2d');

    this.resizeCanvas();
    this.initParticles();

    window.addEventListener('resize', this.onResize);
    window.addEventListener('mousemove', this.onMouseMove);

    // Run animation outside Angular zone for high 60FPS performance
    this.ngZone.runOutsideAngular(() => {
      this.animate();
    });
  }

  ngOnDestroy(): void {
    window.removeEventListener('resize', this.onResize);
    window.removeEventListener('mousemove', this.onMouseMove);

    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
    }
  }

  private onResize = (): void => {
    this.resizeCanvas();
    this.initParticles();
  };

  private onMouseMove = (e: MouseEvent): void => {
    this.mouse.targetX = e.clientX;
    this.mouse.targetY = e.clientY;
  };

  private resizeCanvas(): void {
    const canvas = this.canvasRef.nativeElement;
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;
  }

  private initParticles(): void {
    const canvas = this.canvasRef.nativeElement;
    const count = Math.min(Math.floor((canvas.width * canvas.height) / 18000), 75);
    this.particles = [];

    for (let i = 0; i < count; i++) {
      const baseAlpha = 0.15 + Math.random() * 0.35;
      this.particles.push({
        x: Math.random() * canvas.width,
        y: Math.random() * canvas.height,
        vx: (Math.random() - 0.5) * 0.6,
        vy: (Math.random() - 0.5) * 0.6,
        radius: 1.5 + Math.random() * 2,
        alpha: baseAlpha,
        baseAlpha
      });
    }
  }

  private animate = (): void => {
    if (!this.ctx) return;
    const canvas = this.canvasRef.nativeElement;
    const width = canvas.width;
    const height = canvas.height;

    // Smooth mouse lerp
    this.mouse.x += (this.mouse.targetX - this.mouse.x) * 0.05;
    this.mouse.y += (this.mouse.targetY - this.mouse.y) * 0.05;

    this.ctx.clearRect(0, 0, width, height);

    // Draw connecting lines between particles
    for (let i = 0; i < this.particles.length; i++) {
      const p1 = this.particles[i];

      // Move particle
      p1.x += p1.vx;
      p1.y += p1.vy;

      if (p1.x < 0 || p1.x > width) p1.vx *= -1;
      if (p1.y < 0 || p1.y > height) p1.vy *= -1;

      // Mouse influence
      const dx = this.mouse.x - p1.x;
      const dy = this.mouse.y - p1.y;
      const dist = Math.sqrt(dx * dx + dy * dy);

      if (dist < 150) {
        p1.alpha = p1.baseAlpha + (1 - dist / 150) * 0.5;
      } else {
        p1.alpha += (p1.baseAlpha - p1.alpha) * 0.05;
      }

      // Draw particle
      this.ctx.beginPath();
      this.ctx.arc(p1.x, p1.y, p1.radius, 0, Math.PI * 2);
      this.ctx.fillStyle = `rgba(99, 102, 241, ${p1.alpha})`;
      this.ctx.shadowBlur = 10;
      this.ctx.shadowColor = 'rgba(99, 102, 241, 0.5)';
      this.ctx.fill();

      // Connect lines
      for (let j = i + 1; j < this.particles.length; j++) {
        const p2 = this.particles[j];
        const distance = Math.sqrt((p1.x - p2.x) ** 2 + (p1.y - p2.y) ** 2);

        if (distance < 130) {
          const lineAlpha = (1 - distance / 130) * 0.15;
          this.ctx.beginPath();
          this.ctx.moveTo(p1.x, p1.y);
          this.ctx.lineTo(p2.x, p2.y);
          this.ctx.strokeStyle = `rgba(139, 92, 246, ${lineAlpha})`;
          this.ctx.lineWidth = 1;
          this.ctx.stroke();
        }
      }
    }

    this.animationFrameId = requestAnimationFrame(this.animate);
  };
}
