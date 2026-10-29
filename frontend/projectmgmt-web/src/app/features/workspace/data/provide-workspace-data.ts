import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpWorkspaceDataSource } from './http-workspace-data-source';
import { HybridWorkspaceDataSource } from './hybrid-workspace-data-source';
import { MOCK_WORKSPACE_OPTIONS, MockWorkspaceDataSource } from './mock-workspace-data-source';
import { WorkspaceDataSource } from './workspace-data-source';

/**
 * - `hybrid` (mặc định): API thật cho tổ chức, dự án, tạo dự án, việc được giao; kho phụ trong trình duyệt cho phần backend chưa có.
 * - `mock`: toàn bộ là dữ liệu mẫu giả lập DB (demo không cần backend).
 * - `http`: toàn bộ theo hợp đồng API "Workspace API contract".
 */
export type WorkspaceDataSourceKind = 'mock' | 'hybrid' | 'http';

export function provideWorkspaceData(kind: WorkspaceDataSourceKind = environment.workspaceDataSource): EnvironmentProviders {
  if (kind === 'http') return makeEnvironmentProviders([{ provide: WorkspaceDataSource, useClass: HttpWorkspaceDataSource }]);
  if (kind === 'mock') return makeEnvironmentProviders([{ provide: WorkspaceDataSource, useClass: MockWorkspaceDataSource }]);
  return makeEnvironmentProviders([
    { provide: MOCK_WORKSPACE_OPTIONS, useValue: { seed: false, storagePrefix: 'pm-workspace-shadow-v1:' } },
    MockWorkspaceDataSource,
    { provide: WorkspaceDataSource, useClass: HybridWorkspaceDataSource }
  ]);
}
