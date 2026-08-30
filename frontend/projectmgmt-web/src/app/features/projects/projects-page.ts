import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeaturePlaceholderComponent } from '../../shared/components/feature-placeholder/feature-placeholder';

@Component({ selector: 'app-projects-page', imports: [FeaturePlaceholderComponent], template: '<app-feature-placeholder title="Projects" description="Không gian Organization, Project và cấu hình workflow của module M2/M3." />', changeDetection: ChangeDetectionStrategy.OnPush })
export class ProjectsPage {}
