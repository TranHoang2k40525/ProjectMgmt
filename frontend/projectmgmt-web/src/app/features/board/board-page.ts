import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeaturePlaceholderComponent } from '../../shared/components/feature-placeholder/feature-placeholder';

@Component({ selector: 'app-board-page', imports: [FeaturePlaceholderComponent], template: '<app-feature-placeholder title="Scrum Board" description="Board theo workflow, WIP limit và cập nhật realtime sẽ được bổ sung ở sprint sau." />', changeDetection: ChangeDetectionStrategy.OnPush })
export class BoardPage {}
