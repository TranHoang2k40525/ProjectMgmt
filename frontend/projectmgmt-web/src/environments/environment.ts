export const environment = {
  production: false,
  apiBaseUrl: 'http://projectmgmt_develop.dev.com/api',
  hubBaseUrl: 'http://projectmgmt_develop.dev.com/hubs',
  /** Nguồn dữ liệu không gian Tổ chức → Dự án: 'mock' (giả lập DB trong trình duyệt) hoặc 'http'. */
  workspaceDataSource: 'mock' as 'mock' | 'http'
};
