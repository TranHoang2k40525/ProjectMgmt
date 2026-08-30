import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeaturePlaceholderComponent } from '../../shared/components/feature-placeholder/feature-placeholder';

@Component({ selector: 'app-auth-page', imports: [FeaturePlaceholderComponent], template: '<app-feature-placeholder title="Identity & Access" description="Đăng nhập, OTP, OAuth và RBAC theo phạm vi sẽ nằm trong module M1." />', changeDetection: ChangeDetectionStrategy.OnPush })
export class AuthPage {}
