export function apiErrorMessage(error: unknown, fallback: string): string {
  if (!error || typeof error !== 'object') return fallback;
  const value = error as Record<string, unknown>;
  const body = value['error'] && typeof value['error'] === 'object'
    ? value['error'] as Record<string, unknown> : value;
  for (const key of ['detail', 'title', 'message']) {
    if (typeof body[key] === 'string' && body[key]) return body[key] as string;
  }
  return fallback;
}
