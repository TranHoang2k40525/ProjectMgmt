/** Kiểu dùng chung cho các section con của Issue Detail (style của trang cha không xuyên vào component con). */
export const ISSUE_SECTION_STYLES = `
  :host { display: block; color: #172b4d; }
  h2 { font-size: 1.1rem; font-weight: 650; margin-bottom: .75rem; display: flex; align-items: center; gap: .5rem; }
  .muted { color: #5e6c84; font-size: .85rem; }
  .error { color: #b42318; }
  .row { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
  textarea, input, select { border: 1px solid #aebdd0; border-radius: 6px; padding: .5rem .6rem; color: #172b4d; background: white; max-width: 100%; }
  textarea { width: 100%; }
  button { padding: .45rem .9rem; border-radius: 6px; background: #175f47; color: white; font-weight: 600; }
  button.link { background: none; color: #1559a1; padding: .1rem .3rem; font-weight: 500; text-decoration: underline; }
  button.danger { background: none; color: #b42318; padding: .1rem .3rem; font-weight: 500; }
  button:disabled { opacity: .5; cursor: not-allowed; }
  .preserve { white-space: pre-wrap; word-break: break-word; }
`;
