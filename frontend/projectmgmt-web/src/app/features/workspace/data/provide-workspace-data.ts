import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpWorkspaceDataSource } from './http-workspace-data-source';
import { MockWorkspaceDataSource } from './mock-workspace-data-source';
import { WorkspaceDataSource } from './workspace-data-source';

export type WorkspaceDataSourceKind = 'mock' | 'http';

/** Chọn nguồn dữ liệu cho không gian Tổ chức → Dự án theo `environment.workspaceDataSource`. */
export function provideWorkspaceData(kind: WorkspaceDataSourceKind = environment.workspaceDataSource): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: WorkspaceDataSource, useClass: kind === 'http' ? HttpWorkspaceDataSource : MockWorkspaceDataSource }
  ]);
}
