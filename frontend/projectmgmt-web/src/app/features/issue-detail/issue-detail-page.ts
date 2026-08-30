import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeaturePlaceholderComponent } from '../../shared/components/feature-placeholder/feature-placeholder';

@Component({ selector: 'app-issue-detail-page', imports: [FeaturePlaceholderComponent], template: '<app-feature-placeholder title="Issue Detail" description="Chi tiết Issue, lịch sử, comment, attachment và Acceptance Criteria thuộc module M5." />', changeDetection: ChangeDetectionStrategy.OnPush })
export class IssueDetailPage {}
