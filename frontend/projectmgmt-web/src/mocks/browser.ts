import { setupWorker } from 'msw/browser';
import { workspaceHandlers } from './workspace/handlers';

/** Service worker MSW cho các endpoint backend chưa có; bật bằng `environment.mockMissingApis`. */
export const worker = setupWorker(...workspaceHandlers);

export function startMockWorker(): Promise<unknown> {
  return worker.start({
    // Request không có handler (API backend đã có) đi thẳng tới backend thật, không cảnh báo.
    onUnhandledFrame: 'bypass',
    quiet: true,
    serviceWorker: { url: '/mockServiceWorker.js' }
  });
}
