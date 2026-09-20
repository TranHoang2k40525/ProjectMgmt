import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { IdentityService } from '../../core/services/identity.service';
import { NeoCampusSceneComponent } from '../../shared/scenes/neo-campus-scene/neo-campus-scene';

import { gsap } from 'gsap';

export type AuthMode = 'LOGIN' | 'SIGNUP' | 'OTP_REGISTER' | 'FORGOT' | 'OTP_FORGOT';

@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, NeoCampusSceneComponent],
  templateUrl: './auth-page.html',
  styleUrls: ['./auth-page.scss']
})
export class AuthPageComponent implements OnInit, AfterViewInit, OnDestroy {
  private fb = inject(FormBuilder);
  private identityService = inject(IdentityService);
  private router = inject(Router);

  @ViewChild('authPage', { static: true }) private authPage!: ElementRef<HTMLElement>;
  private motionMedia: ReturnType<typeof gsap.matchMedia> | null = null;
  private modeTween: gsap.core.Tween | null = null;

  readonly mode = signal<AuthMode>('LOGIN');
  readonly loading = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly otpCountdown = signal<number>(0);
  private timerInterval: ReturnType<typeof setInterval> | null = null;

  // Form Groups
  loginForm!: FormGroup;
  signupForm!: FormGroup;
  forgotForm!: FormGroup;
  otpForm!: FormGroup;

  // OTP Array state for 6 pin boxes
  otpPins: string[] = ['', '', '', '', '', ''];

  ngOnInit(): void {
    this.loginForm = this.fb.group({
      email: ['admin@scrumai.internal', [Validators.required, Validators.email]],
      password: ['password123', [Validators.required, Validators.minLength(6)]]
    });

    this.signupForm = this.fb.group({
      displayName: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });

    this.forgotForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });

    this.otpForm = this.fb.group({
      code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
      newPassword: ['']
    });
  }

  ngAfterViewInit(): void {
    const root = this.authPage.nativeElement;
    this.motionMedia = gsap.matchMedia(root);
    this.motionMedia.add(
      {
        animate: '(prefers-reduced-motion: no-preference)',
        desktop: '(min-width: 1024px)',
        compact: '(max-width: 1023px)'
      },
      context => {
        if (!context.conditions?.['animate']) return;

        const isDesktop = context.conditions?.['desktop'];
        const timeline = gsap.timeline({ defaults: { ease: 'power3.out' } });
        timeline
          .from(root.querySelector('.auth-brand'), { autoAlpha: 0, y: -18, duration: 0.65 })
          .from(root.querySelector('.auth-hero-content'), { autoAlpha: 0, x: isDesktop ? -34 : 0, y: isDesktop ? 0 : 18, duration: 0.8 }, '-=0.4')
          .from(root.querySelectorAll('.auth-stat'), { autoAlpha: 0, y: 16, stagger: 0.08, duration: 0.48 }, '-=0.45')
          .from(root.querySelector('.auth-card'), { autoAlpha: 0, x: isDesktop ? 38 : 0, y: isDesktop ? 0 : 22, rotateY: isDesktop ? -3 : 0, duration: 0.85 }, '-=0.6')
          .from(root.querySelector('.auth-footer'), { autoAlpha: 0, y: 10, duration: 0.45 }, '-=0.35');

        return () => timeline.kill();
      }
    );
  }

  ngOnDestroy(): void {
    this.stopTimer();
    this.modeTween?.kill();
    this.motionMedia?.revert();
  }

  setMode(newMode: AuthMode): void {
    this.mode.set(newMode);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (newMode === 'OTP_REGISTER' || newMode === 'OTP_FORGOT') {
      this.startOtpTimer(300);
    } else {
      this.stopTimer();
    }

    setTimeout(() => {
      const body = this.authPage?.nativeElement.querySelector<HTMLElement>('.auth-card-body');
      if (!body || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

      this.modeTween?.kill();
      this.modeTween = gsap.fromTo(
        body,
        { autoAlpha: 0, y: 12, scale: 0.99 },
        { autoAlpha: 1, y: 0, scale: 1, duration: 0.42, ease: 'power2.out', clearProps: 'transform' }
      );
    }, 0);
  }

  // --- PASSWORD STRENGTH CALCULATION ---
  calculatePasswordStrength(password: string): { score: number; label: string; color: string } {
    if (!password) return { score: 0, label: 'Chưa nhập', color: '#64748b' };
    let score = 0;
    if (password.length >= 6) score += 25;
    if (password.length >= 10) score += 25;
    if (/[A-Z]/.test(password)) score += 25;
    if (/[0-9!@#$%^&*]/.test(password)) score += 25;

    if (score <= 25) return { score, label: 'Yếu', color: '#ef4444' };
    if (score <= 50) return { score, label: 'Trung bình', color: '#f59e0b' };
    if (score <= 75) return { score, label: 'Khá tốt', color: '#3b82f6' };
    return { score, label: 'Rất mạnh', color: '#10b981' };
  }

  // --- HANDLERS ---
  onLogin(): void {
    if (this.loginForm.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.loginForm.value;
    this.identityService.login(email, password).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/profile']);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.title || 'Đăng nhập không thành công.');
      }
    });
  }

  onSignup(): void {
    if (this.signupForm.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);

    const { email } = this.signupForm.value;
    this.identityService.signup(this.signupForm.value.displayName, email, this.signupForm.value.password).subscribe({
      next: () => {
        this.loading.set(false);
        this.successMessage.set(`Mã OTP đã được gửi đến ${email}. Mã mẫu test nhanh: 123456`);
        this.setMode('OTP_REGISTER');
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.title || 'Đăng ký không thành công.');
      }
    });
  }

  onSendForgotOtp(): void {
    if (this.forgotForm.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);

    const email = this.forgotForm.value.email;
    this.identityService.sendOtp(email, 'FORGOT_PASSWORD').subscribe({
      next: (res) => {
        this.loading.set(false);
        this.successMessage.set(res.message);
        this.setMode('OTP_FORGOT');
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.title || 'Không thể gửi mã OTP.');
      }
    });
  }

  onOtpInput(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const val = input.value;
    if (val.length > 1) {
      this.otpPins[index] = val.substring(val.length - 1);
    } else {
      this.otpPins[index] = val;
    }

    if (val && index < 5) {
      const nextInput = input.nextElementSibling as HTMLInputElement;
      if (nextInput) nextInput.focus();
    }

    const fullCode = this.otpPins.join('');
    this.otpForm.patchValue({ code: fullCode });
  }

  onOtpKeyDown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.otpPins[index] && index > 0) {
      const prevInput = (event.target as HTMLInputElement).previousElementSibling as HTMLInputElement;
      if (prevInput) prevInput.focus();
    }
  }

  onVerifyOtp(): void {
    const fullCode = this.otpPins.join('');
    if (fullCode.length < 6) {
      this.errorMessage.set('Vui lòng nhập đủ 6 chữ số mã OTP.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const email = this.mode() === 'OTP_REGISTER' ? this.signupForm.value.email : this.forgotForm.value.email;
    const purpose = this.mode() === 'OTP_REGISTER' ? 'REGISTER' : 'FORGOT_PASSWORD';

    this.identityService.verifyOtp(email, fullCode, purpose).subscribe({
      next: () => {
        this.loading.set(false);
        if (purpose === 'REGISTER') {
          this.router.navigate(['/profile']);
        } else {
          this.successMessage.set('Xác thực thành công! Vui lòng thiết lập mật khẩu mới.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.title || 'Xác thực OTP thất bại.');
      }
    });
  }

  onResetPassword(): void {
    const newPass = this.otpForm.value.newPassword;
    const fullCode = this.otpPins.join('');
    const email = this.forgotForm.value.email;

    if (!newPass || newPass.length < 6) {
      this.errorMessage.set('Mật khẩu mới phải có ít nhất 6 ký tự.');
      return;
    }

    this.loading.set(true);
    this.identityService.resetPassword(email, fullCode, newPass).subscribe({
      next: () => {
        this.loading.set(false);
        this.successMessage.set('Đổi mật khẩu thành công! Vui lòng đăng nhập lại.');
        this.setMode('LOGIN');
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.title || 'Đổi mật khẩu thất bại.');
      }
    });
  }

  resendOtp(): void {
    const email = this.mode() === 'OTP_REGISTER' ? this.signupForm.value.email : this.forgotForm.value.email;
    const purpose = this.mode() === 'OTP_REGISTER' ? 'REGISTER' : 'FORGOT_PASSWORD';

    this.identityService.sendOtp(email, purpose).subscribe({
      next: (res) => {
        this.successMessage.set(res.message);
        this.startOtpTimer(300);
      }
    });
  }

  private startOtpTimer(seconds: number): void {
    this.stopTimer();
    this.otpCountdown.set(seconds);
    this.timerInterval = setInterval(() => {
      if (this.otpCountdown() > 0) {
        this.otpCountdown.update(v => v - 1);
      } else {
        this.stopTimer();
      }
    }, 1000);
  }

  private stopTimer(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = null;
    }
  }

  formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }
}
