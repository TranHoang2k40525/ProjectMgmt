import { environment } from '../../../environments/environment';

/**
 * Chuẩn hóa URL ảnh đại diện (avatar) từ backend.
 * Nếu là đường dẫn tương đối (ví dụ /assets/avatars/xxx.png), tự động nối với domain của backend API.
 */
export function resolveAvatarUrl(url?: string | null): string | null {
  if (!url || typeof url !== 'string') {
    return null;
  }

  const trimmed = url.trim();
  if (!trimmed) {
    return null;
  }

  // Nếu đã là URL tuyệt đối (http/https/data URI/blob), giữ nguyên
  if (/^(https?:|\/\/|data:|blob:)/i.test(trimmed)) {
    return trimmed;
  }

  // Lấy host của backend từ apiBaseUrl (ví dụ http://localhost:5083/api -> http://localhost:5083)
  const backendHost = (environment.apiBaseUrl || '').replace(/\/api\/?$/, '');
  const cleanPath = trimmed.startsWith('/') ? trimmed : `/${trimmed}`;

  return `${backendHost}${cleanPath}`;
}
