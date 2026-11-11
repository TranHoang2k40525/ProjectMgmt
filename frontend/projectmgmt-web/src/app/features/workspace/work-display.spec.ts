import { describe, expect, it } from 'vitest';
import { WorkItemSummary } from './data/workspace.models';
import { formatDueDate, groupWorkByDue, initials, timeAgo } from './work-display';

function item(id: string, dueDate: string | null, statusCategory: WorkItemSummary['statusCategory'] = 'ToDo'): WorkItemSummary {
  return {
    id, issueKey: id, title: id, projectId: 'p', projectName: 'P', orgId: 'o', orgName: 'O',
    statusName: 'To Do', statusCategory, priority: 'Medium', dueDate
  };
}

describe('work display helpers', () => {
  it('groups open work by due date and skips finished work', () => {
    const today = new Date(2026, 9, 29, 15, 0);
    const buckets = groupWorkByDue([
      item('late', '2026-10-28'), item('now', '2026-10-29'), item('soon', '2026-11-02'),
      item('free', null), item('done', '2026-10-01', 'Done')
    ], today);
    expect(buckets.map(bucket => [bucket.key, bucket.items.map(work => work.id)])).toEqual([
      ['overdue', ['late']], ['today', ['now']], ['upcoming', ['soon']], ['noDue', ['free']]
    ]);
  });

  it('formats dates and relative times in Vietnamese', () => {
    const now = new Date('2026-10-29T10:00:00Z');
    expect(formatDueDate('2026-11-02')).toBe('02/11/2026');
    expect(formatDueDate(null)).toBe('Chưa có hạn');
    expect(timeAgo('2026-10-29T09:59:40Z', now)).toBe('vừa xong');
    expect(timeAgo('2026-10-29T09:15:00Z', now)).toBe('45 phút trước');
    expect(timeAgo('2026-10-27T10:00:00Z', now)).toBe('2 ngày trước');
  });

  it('builds avatar initials from the first and last word', () => {
    expect(initials('Nguyễn Minh Anh')).toBe('NA');
    expect(initials('Linh')).toBe('LI');
  });
});
