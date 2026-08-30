import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeaturePlaceholderComponent } from '../../shared/components/feature-placeholder/feature-placeholder';

@Component({ selector: 'app-reports-page', imports: [FeaturePlaceholderComponent], template: '<app-feature-placeholder title="Reports" description="Burndown, Velocity và báo cáo chất lượng AI sẽ dùng các read-model chỉ đọc." />', changeDetection: ChangeDetectionStrategy.OnPush })
export class ReportsPage {}
