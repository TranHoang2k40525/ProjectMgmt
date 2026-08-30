import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeaturePlaceholderComponent } from '../../shared/components/feature-placeholder/feature-placeholder';

@Component({ selector: 'app-ai-dataops-page', imports: [FeaturePlaceholderComponent], template: '<app-feature-placeholder title="AI DataOps" description="Dataset, cleaning rules, training run và evaluation thuộc module M9." />', changeDetection: ChangeDetectionStrategy.OnPush })
export class AiDataOpsPage {}
