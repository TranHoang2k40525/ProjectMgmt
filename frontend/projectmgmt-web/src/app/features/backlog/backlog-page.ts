import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeaturePlaceholderComponent } from '../../shared/components/feature-placeholder/feature-placeholder';

@Component({ selector: 'app-backlog-page', imports: [FeaturePlaceholderComponent], template: '<app-feature-placeholder title="Backlog & Sprint" description="Lập kế hoạch Sprint, capacity và AI Breakdown sẽ được tích hợp tại đây." />', changeDetection: ChangeDetectionStrategy.OnPush })
export class BacklogPage {}
