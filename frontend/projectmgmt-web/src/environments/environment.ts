export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5083/api',
  hubBaseUrl: 'http://localhost:5083/hubs',
  /** Bật MSW cho các endpoint backend chưa có (thành viên, lời mời, link mời, mẫu workflow tổ chức, Gần đây). */
  mockMissingApis: false
};
