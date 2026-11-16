import { HttpErrorResponse } from '@angular/common/http';
import { apiErrorMessage } from '../../core/http/api-error-message';

/**
 * Lỗi tới màn hình có hai dạng: `ApiError` ({ status, code, title, detail }) do errorInterceptor chuẩn hóa từ API thật,
 * hoặc `HttpErrorResponse` (body ProblemDetails có `code`) do kho mock tạo. Hai hàm dưới đọc được cả hai.
 */
type ErrorShape = { status?: number; code?: string; title?: string; detail?: string; error?: { code?: string; title?: string; detail?: string; message?: string } | null };

/** statusText mặc định của HttpClient — không phải thông báo của server. */
const GENERIC_TITLES = new Set(['OK', 'Bad Request', 'Unauthorized', 'Forbidden', 'Not Found', 'Conflict', 'Internal Server Error', 'Unknown Error', 'Request failed', 'Unexpected client error']);

function shape(error: unknown): ErrorShape {
  return error && typeof error === 'object' ? error as ErrorShape : {};
}

export function workspaceErrorCode(error: unknown): string {
  const value = shape(error);
  const nested = value.error && typeof value.error === 'object' ? value.error.code : undefined;
  return nested ?? (error instanceof HttpErrorResponse ? '' : value.code ?? '');
}

/** Như apiErrorMessage, nhưng lỗi mạng (status 0) và lỗi 5xx không có nội dung thì dùng câu dễ hiểu thay cho thông báo kỹ thuật. */
export function workspaceErrorMessage(error: unknown, fallback: string): string {
  const value = shape(error);
  if (value.status === 0) return 'Không kết nối được máy chủ. Kiểm tra backend đang chạy rồi thử lại.';

  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    const hasText = body && typeof body === 'object' && ['detail', 'title', 'message'].some(key => typeof (body as Record<string, unknown>)[key] === 'string');
    if (!hasText) return error.status >= 500 ? 'Máy chủ đang gặp lỗi. Thử lại sau ít phút.' : fallback;
    return apiErrorMessage(error, fallback);
  }

  // ApiError từ errorInterceptor: không có nội dung từ server thì `detail` là thông báo kỹ thuật của HttpClient.
  const text = [value.detail, value.title].find(item => typeof item === 'string' && item && !item.startsWith('Http failure') && !GENERIC_TITLES.has(item));
  if (text) return text;
  return (value.status ?? 0) >= 500 ? 'Máy chủ đang gặp lỗi. Thử lại sau ít phút.' : fallback;
}
