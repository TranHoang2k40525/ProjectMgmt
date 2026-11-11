import { describe, expect, it } from 'vitest';
import { IssueCoreDto } from '../../../core/api/issue-api.models';
import { IssueFormValue, toFormValue, toUpdateRequest, validateIssueForm } from './issue-form.model';

const valid: IssueFormValue = {
  title: 'Tạo màn hình đăng nhập', description: '', assigneeId: '', priorityId: '',
  storyPoints: 3, estimateHours: 1.5, dueDate: '2026-10-30', parentId: ''
};

describe('issue form model', () => {
  it('chấp nhận dữ liệu hợp lệ', () => {
    expect(validateIssueForm(valid)).toBeNull();
  });

  it('chặn tiêu đề rỗng, quá dài và story points sai', () => {
    expect(validateIssueForm({ ...valid, title: '   ' })).toContain('không được để trống');
    expect(validateIssueForm({ ...valid, title: 'x'.repeat(501) })).toContain('500');
    expect(validateIssueForm({ ...valid, storyPoints: -1 })).toContain('Story points');
    expect(validateIssueForm({ ...valid, storyPoints: 1.255 })).toContain('2 chữ số');
    expect(validateIssueForm({ ...valid, dueDate: '1999-01-01' })).toContain('Hạn');
  });

  it('đổi giờ ↔ phút và chuỗi rỗng thành null khi gửi', () => {
    const request = toUpdateRequest({ ...valid, title: '  Đổi tên  ' });
    expect(request).toEqual({
      title: 'Đổi tên', description: null, assigneeId: null, priorityId: null,
      storyPoints: 3, originalEstimateMinutes: 90, dueDate: '2026-10-30'
    });

    const form = toFormValue({ id: 'i1', title: 'T', originalEstimateMinutes: 90, parentId: 'p1' } as IssueCoreDto);
    expect(form.estimateHours).toBe(1.5);
    expect(form.parentId).toBe('p1');
    expect(form.assigneeId).toBe('');
  });
});
