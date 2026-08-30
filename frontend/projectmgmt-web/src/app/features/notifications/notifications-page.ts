import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeaturePlaceholderComponent } from '../../shared/components/feature-placeholder/feature-placeholder';

@Component({ selector: 'app-notifications-page', imports: [FeaturePlaceholderComponent], template: '<app-feature-placeholder title="Notifications" description="Notification Center và SignalR groups theo user/project thuộc module M10." />', changeDetection: ChangeDetectionStrategy.OnPush })
export class NotificationsPage {}
