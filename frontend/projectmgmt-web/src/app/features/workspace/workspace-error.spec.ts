import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { workspaceErrorMessage } from './workspace-error';

describe('workspaceErrorMessage', () => {
  it('explains network failures instead of showing the raw HttpClient message', () => {
    const offline = new HttpErrorResponse({ status: 0, url: 'http://api/organizations', error: new ProgressEvent('error') });
    expect(workspaceErrorMessage(offline, 'fallback')).toContain('Không kết nối được máy chủ');
  });

  it('uses the server message when there is one and a friendly text for empty 5xx', () => {
    expect(workspaceErrorMessage(new HttpErrorResponse({ status: 409, error: { title: 'Mã đã được dùng.' } }), 'fallback')).toBe('Mã đã được dùng.');
    expect(workspaceErrorMessage(new HttpErrorResponse({ status: 500, error: null }), 'fallback')).toContain('Máy chủ đang gặp lỗi');
    expect(workspaceErrorMessage(new HttpErrorResponse({ status: 404, error: null }), 'fallback')).toBe('fallback');
  });
});

describe('workspace errors normalized by errorInterceptor', () => {
  it('reads code and message from ApiError objects', async () => {
    const { workspaceErrorCode } = await import('./workspace-error');
    const apiError = { status: 409, code: 'PROJECT_KEY_DUPLICATE', title: 'Conflict', detail: 'Mã đã tồn tại.' };
    expect(workspaceErrorCode(apiError)).toBe('PROJECT_KEY_DUPLICATE');
    expect(workspaceErrorMessage(apiError, 'fallback')).toBe('Mã đã tồn tại.');
    expect(workspaceErrorMessage({ status: 0, code: 'http.error', title: 'Unknown Error', detail: 'Http failure response for x: 0 Unknown Error' }, 'fallback'))
      .toContain('Không kết nối được máy chủ');
    expect(workspaceErrorMessage({ status: 404, code: 'http.error', title: 'Not Found', detail: 'Http failure response for x: 404 Not Found' }, 'fallback'))
      .toBe('fallback');
  });
});
