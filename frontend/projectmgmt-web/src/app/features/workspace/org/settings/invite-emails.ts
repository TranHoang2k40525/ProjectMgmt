import { InviteEmailOutcome } from '../../data/workspace.models';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
export const MAX_INVITE_EMAILS = 20;

export interface ParsedEmails {
  valid: string[];
  invalid: string[];
}

/** Tách danh sách email nhập tay (phẩy, chấm phẩy, khoảng trắng, xuống dòng), bỏ trùng, chữ thường. */
export function parseInviteEmails(text: string): ParsedEmails {
  const seen = new Set<string>();
  const valid: string[] = [];
  const invalid: string[] = [];
  for (const raw of text.split(/[\s,;]+/)) {
    const email = raw.trim().toLowerCase();
    if (!email || seen.has(email)) continue;
    seen.add(email);
    (EMAIL_PATTERN.test(email) ? valid : invalid).push(email);
  }
  return { valid, invalid };
}

export const INVITE_OUTCOME_LABELS: Readonly<Record<InviteEmailOutcome, { label: string; icon: string; tone: string }>> = {
  Sent: { label: 'Đã gửi lời mời', icon: 'check_circle', tone: 'text-emerald-700 dark:text-emerald-400' },
  NoAccount: { label: 'Email chưa có tài khoản', icon: 'person_off', tone: 'text-red-700 dark:text-red-400' },
  AlreadyMember: { label: 'Đã là thành viên', icon: 'how_to_reg', tone: 'text-slate-600 dark:text-slate-300' },
  AlreadyInvited: { label: 'Đã có lời mời đang chờ', icon: 'schedule', tone: 'text-amber-700 dark:text-amber-400' }
};
