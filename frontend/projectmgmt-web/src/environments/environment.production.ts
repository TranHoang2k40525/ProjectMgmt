export const environment = {
  production: true,
  apiBaseUrl: '/api',
  hubBaseUrl: '/hubs',
  /** Bật MSW cho các endpoint backend chưa có (thành viên, lời mời, link mời, mẫu workflow tổ chức, Gần đây). */
  mockMissingApis: true
} as const;
