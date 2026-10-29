import { RecentKind, StatusCategory, WorkItemSummary } from './data/workspace.models';

export type DueBucket = 'overdue' | 'today' | 'upcoming' | 'noDue';

export interface WorkBucket {
  key: DueBucket;
  label: string;
  items: WorkItemSummary[];
}

const BUCKET_LABELS: Record<DueBucket, string> = {
  overdue: 'Quá hạn',
  today: 'Hôm nay',
  upcoming: 'Sắp tới',
  noDue: 'Chưa có hạn'
};

/** Ngày theo giờ máy người dùng dạng yyyy-MM-dd để so với dueDate (không lệch múi giờ như toISOString). */
export function localDateKey(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

/** Nhóm việc chưa xong theo hạn; việc đã hoàn thành không hiện trong "Dành cho bạn". */
export function groupWorkByDue(items: readonly WorkItemSummary[], today: Date = new Date()): WorkBucket[] {
  const todayKey = localDateKey(today);
  const buckets: Record<DueBucket, WorkItemSummary[]> = { overdue: [], today: [], upcoming: [], noDue: [] };
  for (const item of items) {
    if (item.statusCategory === 'Done') continue;
    if (!item.dueDate) buckets.noDue.push(item);
    else if (item.dueDate < todayKey) buckets.overdue.push(item);
    else if (item.dueDate === todayKey) buckets.today.push(item);
    else buckets.upcoming.push(item);
  }
  return (Object.keys(buckets) as DueBucket[])
    .filter(key => buckets[key].length > 0)
    .map(key => ({ key, label: BUCKET_LABELS[key], items: buckets[key] }));
}

export interface WorkGroup {
  key: string;
  label: string;
  /** Nhóm theo hạn thì là loại hạn, nhóm theo dự án thì null. */
  due: DueBucket | null;
  items: WorkItemSummary[];
}

/** Nhóm việc chưa xong theo dự án (giữ thứ tự hạn trong từng dự án). */
export function groupWorkByProject(items: readonly WorkItemSummary[]): WorkGroup[] {
  const groups = new Map<string, WorkGroup>();
  for (const item of items) {
    if (item.statusCategory === 'Done') continue;
    const group = groups.get(item.projectId) ?? { key: item.projectId, label: item.projectName, due: null, items: [] };
    group.items.push(item);
    groups.set(item.projectId, group);
  }
  return [...groups.values()];
}

export function dueBucketOf(dueDate: string | null, today: Date = new Date()): DueBucket {
  if (!dueDate) return 'noDue';
  const todayKey = localDateKey(today);
  return dueDate < todayKey ? 'overdue' : dueDate === todayKey ? 'today' : 'upcoming';
}

export function formatDueDate(dueDate: string | null): string {
  if (!dueDate) return 'Chưa có hạn';
  const [year, month, day] = dueDate.split('-');
  return `${day}/${month}/${year}`;
}

/** "vừa xong", "5 phút trước", "3 giờ trước", "2 ngày trước", quá 7 ngày thì hiện ngày. */
export function timeAgo(isoDate: string, now: Date = new Date()): string {
  const diff = Math.max(0, now.getTime() - new Date(isoDate).getTime());
  const minutes = Math.floor(diff / 60_000);
  if (minutes < 1) return 'vừa xong';
  if (minutes < 60) return `${minutes} phút trước`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} giờ trước`;
  const days = Math.floor(hours / 24);
  if (days <= 7) return `${days} ngày trước`;
  return new Date(isoDate).toLocaleDateString('vi-VN');
}

export const RECENT_ICONS: Readonly<Record<RecentKind, string>> = {
  organization: 'domain',
  project: 'folder',
  issue: 'task_alt'
};

export const RECENT_KIND_LABELS: Readonly<Record<RecentKind, string>> = {
  organization: 'Tổ chức',
  project: 'Dự án',
  issue: 'Công việc'
};

export function statusTone(category: StatusCategory): string {
  return category === 'Done'
    ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
    : category === 'InProgress'
      ? 'bg-blue-50 text-blue-700 border-blue-200'
      : 'bg-slate-100 text-slate-700 border-slate-200';
}

export function initials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  const letters = words.length === 1 ? words[0].slice(0, 2) : words[0][0] + words[words.length - 1][0];
  return letters.toUpperCase();
}
