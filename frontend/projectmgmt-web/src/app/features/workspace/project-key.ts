export const PROJECT_KEY_PATTERN = /^[A-Z][A-Z0-9]{1,9}$/;
const MAX_KEY_LENGTH = 10;

/** Bỏ dấu tiếng Việt (kể cả đ/Đ) để dùng cho mã, slug và tìm kiếm. */
export function stripDiacritics(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D');
}

/**
 * Gợi ý mã dự án từ tên: nhiều từ lấy chữ cái đầu mỗi từ, một từ lấy 4 ký tự đầu.
 * Kết quả viết hoa, 2–10 ký tự, bắt đầu bằng chữ cái; tên trống trả chuỗi rỗng.
 */
export function suggestProjectKey(name: string): string {
  const words = stripDiacritics(name)
    .toUpperCase()
    .split(/[^A-Z0-9]+/)
    .filter(word => word.length > 0);
  if (words.length === 0) return '';

  let key = words.length === 1 ? words[0].slice(0, 4) : words.map(word => word[0]).join('');
  if (key.length < 2) key = words.join('').slice(0, 4);
  if (/^[0-9]/.test(key)) key = `P${key}`;
  if (key.length < 2) key = key.padEnd(2, 'X');
  return key.slice(0, MAX_KEY_LENGTH);
}

export function normalizeProjectKey(value: string): string {
  return stripDiacritics(value).toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, MAX_KEY_LENGTH);
}

export function isValidProjectKey(value: string): boolean {
  return PROJECT_KEY_PATTERN.test(value);
}

/** Slug tổ chức: chữ thường, số và gạch nối. */
export function suggestSlug(name: string): string {
  return stripDiacritics(name)
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 60);
}

/** So khớp tìm kiếm không phân biệt hoa thường và dấu. */
export function matchesSearch(text: string, query: string): boolean {
  const needle = stripDiacritics(query).toLowerCase().trim();
  return needle.length === 0 || stripDiacritics(text).toLowerCase().includes(needle);
}
