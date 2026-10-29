/** Lớp Tailwind dùng chung cho các màn không gian làm việc, để nút/ô nhập đồng nhất. */
export const UI = {
  primaryButton:
    'inline-flex items-center justify-center gap-1.5 h-9 px-4 rounded-lg bg-primary-container text-white text-sm font-semibold shadow-sm hover:bg-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-container disabled:opacity-50 disabled:cursor-not-allowed',
  secondaryButton:
    'inline-flex items-center justify-center gap-1.5 h-9 px-4 rounded-lg bg-slate-100 text-slate-800 text-sm font-semibold hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-100 dark:hover:bg-slate-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-container disabled:opacity-50 disabled:cursor-not-allowed',
  ghostButton:
    'inline-flex items-center justify-center gap-1.5 h-8 px-3 rounded-lg text-slate-600 text-sm font-medium hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800 focus-visible:outline-2 focus-visible:outline-primary-container disabled:opacity-50 disabled:cursor-not-allowed',
  dangerButton:
    'inline-flex items-center justify-center gap-1.5 h-8 px-3 rounded-lg text-red-700 text-sm font-medium hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-950/40 focus-visible:outline-2 focus-visible:outline-red-600 disabled:opacity-50 disabled:cursor-not-allowed',
  input:
    'h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary-container focus:border-transparent aria-[invalid=true]:border-red-500 dark:bg-slate-950 dark:border-slate-700 dark:text-slate-100',
  textarea:
    'w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary-container focus:border-transparent aria-[invalid=true]:border-red-500 dark:bg-slate-950 dark:border-slate-700 dark:text-slate-100',
  select:
    'h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary-container dark:bg-slate-950 dark:border-slate-700 dark:text-slate-100',
  label: 'block text-sm font-semibold text-slate-800 dark:text-slate-200 mb-1',
  hint: 'mt-1 text-xs text-slate-500 text-pretty',
  fieldError: 'mt-1 text-xs font-medium text-red-700 dark:text-red-400',
  card: 'rounded-2xl border border-slate-200 bg-white p-4 sm:p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900',
  sectionTitle: 'text-base font-bold text-slate-900 dark:text-white text-balance'
} as const;
