export const environment = {
  production: true,
  apiBaseUrl: '/api',
  hubBaseUrl: '/hubs',
  workspaceDataSource: 'hybrid' as 'mock' | 'hybrid' | 'http'
} as const;
