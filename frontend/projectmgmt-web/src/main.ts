import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { environment } from './environments/environment';

/** MSW giả lập các endpoint backend chưa có (xem src/mocks); tắt bằng `environment.mockMissingApis`. */
async function startMocks(): Promise<void> {
  if (!environment.mockMissingApis) return;
  const { startMockWorker } = await import('./mocks/browser');
  await startMockWorker();
}

startMocks()
  .catch(error => console.error('Không khởi động được MSW:', error))
  .then(() => bootstrapApplication(App, appConfig))
  .catch((err) => console.error(err));
