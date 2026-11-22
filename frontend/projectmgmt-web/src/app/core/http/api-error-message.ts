export function apiErrorMessage(error: unknown, fallback: string): string {
  if (!error || typeof error !== 'object') return fallback;
  const value = error as Record<string, unknown>;

  const status = value['status'];
  if (status === 403) {
    return 'Bạn không có quyền thực hiện thao tác này (Chỉ dành cho Quản lý dự án hoặc cấp cao hơn).';
  }
  if (status === 401) {
    return 'Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại.';
  }

  const body = value['error'] && typeof value['error'] === 'object'
    ? value['error'] as Record<string, unknown> : value;
  for (const key of ['detail', 'title', 'message']) {
    const v = body[key];
    if (typeof v === 'string' && v) {
      if (v.includes('Http failure response for')) {
        continue;
      }
      return v;
    }
  }
  return fallback;
}
