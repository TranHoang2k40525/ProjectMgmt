import { describe, expect, it } from 'vitest';
import { isValidProjectKey, matchesSearch, normalizeProjectKey, suggestProjectKey, suggestSlug } from './project-key';

describe('project key helpers', () => {
  it('takes the first letter of each word without Vietnamese diacritics', () => {
    expect(suggestProjectKey('Hệ thống quản lý')).toBe('HTQL');
    expect(suggestProjectKey('Đặt phòng hội thảo')).toBe('DPHT');
  });

  it('uses the first four letters of a single word', () => {
    expect(suggestProjectKey('Website')).toBe('WEBS');
    expect(suggestProjectKey('AI')).toBe('AI');
  });

  it('prefixes keys that would start with a digit and caps the length', () => {
    expect(suggestProjectKey('2026 cổng thông tin')).toBe('P2CTT');
    expect(suggestProjectKey('a b c d e f g h i j k l')).toHaveLength(10);
  });

  it('returns an empty key for names without letters or digits', () => {
    expect(suggestProjectKey('  ---  ')).toBe('');
  });

  it('normalizes and validates manual keys', () => {
    expect(normalizeProjectKey('qlda-ư 1')).toBe('QLDAU1');
    expect(isValidProjectKey('QLDA')).toBe(true);
    expect(isValidProjectKey('1ABC')).toBe(false);
    expect(isValidProjectKey('A')).toBe(false);
  });

  it('builds slugs and matches searches regardless of accents and case', () => {
    expect(suggestSlug('CLB Lập trình HUCE')).toBe('clb-lap-trinh-huce');
    expect(matchesSearch('Cổng thông tin sinh viên', 'cong thong')).toBe(true);
    expect(matchesSearch('Cổng thông tin sinh viên', 'booking')).toBe(false);
  });
});
