import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { WorkspaceApiDataSource } from './workspace-api-data-source';
import { WorkspaceDataSource } from './workspace-data-source';

/** Không gian Tổ chức → Dự án luôn gọi HTTP; endpoint backend chưa có do MSW giả lập (xem src/mocks). */
export function provideWorkspaceData(): EnvironmentProviders {
  return makeEnvironmentProviders([{ provide: WorkspaceDataSource, useClass: WorkspaceApiDataSource }]);
}
