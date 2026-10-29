export const environment = {
  production: false,
  apiBaseUrl: 'http://projectmgmt_develop.dev.com/api',
  hubBaseUrl: 'http://projectmgmt_develop.dev.com/hubs',
  /**
   * Nguồn dữ liệu không gian Tổ chức → Dự án: 'hybrid' (API thật + kho phụ cho phần backend chưa có),
   * 'mock' (toàn bộ dữ liệu mẫu) hoặc 'http' (toàn bộ API theo hợp đồng).
   */
  workspaceDataSource: 'hybrid' as 'mock' | 'hybrid' | 'http'
};
