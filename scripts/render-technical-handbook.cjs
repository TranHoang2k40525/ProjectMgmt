const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const handbookDir = path.join(repoRoot, 'docs', 'technical-handbook');
const markdownPath = path.join(handbookDir, 'projectmgmt-technical-handbook.md');
const htmlPath = path.join(handbookDir, 'projectmgmt-technical-handbook.html');
const textPath = path.join(handbookDir, 'projectmgmt-technical-handbook.txt');

function escapeHtml(value) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

function slugify(value) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/`/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'section';
}

function inline(value) {
  let escaped = escapeHtml(value);
  const codeSpans = [];
  escaped = escaped.replace(/`([^`]+)`/g, (_, code) => {
    const token = `@@CODE${codeSpans.length}@@`;
    codeSpans.push(`<code>${code}</code>`);
    return token;
  });
  escaped = escaped.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, '<img src="$2" alt="$1" loading="lazy">');
  escaped = escaped.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, label, target) => {
    const href = target.startsWith('#') ? `#${slugify(target.slice(1))}` : target;
    return `<a href="${href}">${label}</a>`;
  });
  escaped = escaped.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  escaped = escaped.replace(/\*([^*]+)\*/g, '<em>$1</em>');
  escaped = escaped.replace(/\[VERIFIED\]/g, '<span class="evidence verified">VERIFIED</span>');
  escaped = escaped.replace(/\[INFERRED\]/g, '<span class="evidence inferred">INFERRED</span>');
  escaped = escaped.replace(/\[RECOMMENDATION\]/g, '<span class="evidence recommendation">RECOMMENDATION</span>');
  escaped = escaped.replace(/@@CODE(\d+)@@/g, (_, index) => codeSpans[Number(index)]);
  return escaped;
}

function splitTableRow(line) {
  return line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(cell => cell.trim());
}

function renderMermaidVisual(source) {
  const lines = source.split('\n').map(line => line.trim()).filter(Boolean);
  const isSequence = lines[0]?.startsWith('sequenceDiagram');
  const labels = new Map();

  if (isSequence) {
    for (const line of lines.slice(1)) {
      const declaration = line.match(/^(?:actor|participant)\s+(\w+)\s+as\s+(.+)$/);
      if (declaration) labels.set(declaration[1], declaration[2]);
    }
    const steps = [];
    for (const line of lines.slice(1)) {
      const message = line.match(/^(\w+)\s*(-->>|->>|-->|->)\s*(\w+)\s*:\s*(.+)$/);
      if (!message) continue;
      steps.push({ from: labels.get(message[1]) || message[1], to: labels.get(message[3]) || message[3], label: message[4] });
    }
    const rows = steps.map((step, index) => `<div class="diagram-row sequence-row"><span class="step">${index + 1}</span><span class="node">${inline(step.from)}</span><span class="arrow">→</span><span class="node">${inline(step.to)}</span><span class="edge-label">${inline(step.label)}</span></div>`).join('');
    return `<figure class="diagram"><figcaption>Sequence — luồng tương tác</figcaption>${rows}<details><summary>Mermaid source</summary><pre><code>${escapeHtml(source)}</code></pre></details></figure>`;
  }

  function registerNodes(line) {
    let normalized = line.replace(/\b(\w+)\[\((.*?)\)\]/g, (_, id, label) => {
      labels.set(id, label);
      return id;
    });
    normalized = normalized.replace(/\b(\w+)\[(.*?)\]/g, (_, id, label) => {
      labels.set(id, label);
      return id;
    });
    normalized = normalized.replace(/\b(\w+)\{(.*?)\}/g, (_, id, label) => {
      labels.set(id, label);
      return id;
    });
    return normalized;
  }

  const relationships = [];
  for (const original of lines.slice(1)) {
    const line = registerNodes(original);
    const edge = line.match(/^(\w+)\s*(<-->|-->|-.+?\.->)\s*(?:\|([^|]+)\|\s*)?(\w+)$/);
    if (!edge) continue;
    const dashedLabel = edge[2].startsWith('-.') ? edge[2].replace(/^-\.\s*/, '').replace(/\s*\.->$/, '') : '';
    relationships.push({ from: edge[1], to: edge[4], label: edge[3] || dashedLabel });
  }
  const rows = relationships.map(item => `<div class="diagram-row"><span class="node">${inline(labels.get(item.from) || item.from)}</span><span class="arrow">→</span><span class="node">${inline(labels.get(item.to) || item.to)}</span>${item.label ? `<span class="edge-label">${inline(item.label)}</span>` : ''}</div>`).join('');
  return `<figure class="diagram"><figcaption>Flow — quan hệ thành phần</figcaption>${rows}<details><summary>Mermaid source</summary><pre><code>${escapeHtml(source)}</code></pre></details></figure>`;
}

function renderMarkdown(markdown) {
  const lines = markdown.replace(/\r\n/g, '\n').split('\n');
  const output = [];
  const headings = [];
  const usedSlugs = new Map();
  let paragraph = [];
  let listType = null;
  let inCode = false;
  let codeLanguage = '';
  let codeLines = [];

  function flushParagraph() {
    if (paragraph.length) {
      output.push(`<p>${inline(paragraph.join(' '))}</p>`);
      paragraph = [];
    }
  }

  function closeList() {
    if (listType) {
      output.push(`</${listType}>`);
      listType = null;
    }
  }

  function uniqueSlug(label) {
    const base = slugify(label);
    const count = usedSlugs.get(base) || 0;
    usedSlugs.set(base, count + 1);
    return count ? `${base}-${count + 1}` : base;
  }

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];

    if (line.startsWith('```')) {
      flushParagraph();
      closeList();
      if (!inCode) {
        inCode = true;
        codeLanguage = line.slice(3).trim();
        codeLines = [];
      } else {
        if (codeLanguage.toLowerCase() === 'mermaid') {
          output.push(renderMermaidVisual(codeLines.join('\n')));
        } else {
          const languageClass = codeLanguage ? ` class="language-${escapeHtml(codeLanguage)}"` : '';
          output.push(`<pre><code${languageClass}>${escapeHtml(codeLines.join('\n'))}</code></pre>`);
        }
        inCode = false;
        codeLanguage = '';
      }
      continue;
    }

    if (inCode) {
      codeLines.push(line);
      continue;
    }

    const headingMatch = line.match(/^(#{1,6})\s+(.+)$/);
    if (headingMatch) {
      flushParagraph();
      closeList();
      const level = headingMatch[1].length;
      const label = headingMatch[2].replace(/`/g, '');
      const slug = uniqueSlug(label);
      headings.push({ level, label, slug });
      output.push(`<h${level} id="${slug}">${inline(headingMatch[2])}</h${level}>`);
      continue;
    }

    if (/^\s*---+\s*$/.test(line)) {
      flushParagraph();
      closeList();
      output.push('<hr>');
      continue;
    }

    if (line.trim().startsWith('|') && index + 1 < lines.length && /^\s*\|?\s*:?-{3,}/.test(lines[index + 1])) {
      flushParagraph();
      closeList();
      const headers = splitTableRow(line);
      index += 2;
      const rows = [];
      while (index < lines.length && lines[index].trim().startsWith('|')) {
        rows.push(splitTableRow(lines[index]));
        index += 1;
      }
      index -= 1;
      const head = headers.map(cell => `<th>${inline(cell)}</th>`).join('');
      const body = rows.map(row => `<tr>${row.map(cell => `<td>${inline(cell)}</td>`).join('')}</tr>`).join('');
      output.push(`<div class="table-wrap"><table><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table></div>`);
      continue;
    }

    const unorderedMatch = line.match(/^\s*[-*]\s+(.+)$/);
    const orderedMatch = line.match(/^\s*\d+\.\s+(.+)$/);
    if (unorderedMatch || orderedMatch) {
      flushParagraph();
      const nextType = unorderedMatch ? 'ul' : 'ol';
      if (listType !== nextType) {
        closeList();
        listType = nextType;
        output.push(`<${listType}>`);
      }
      output.push(`<li>${inline((unorderedMatch || orderedMatch)[1])}</li>`);
      continue;
    }

    const quoteMatch = line.match(/^>\s?(.*)$/);
    if (quoteMatch) {
      flushParagraph();
      closeList();
      output.push(`<blockquote>${inline(quoteMatch[1])}</blockquote>`);
      continue;
    }

    if (!line.trim()) {
      flushParagraph();
      closeList();
      continue;
    }

    if (/^<[^>]+>/.test(line.trim())) {
      flushParagraph();
      closeList();
      output.push(line);
      continue;
    }

    paragraph.push(line.trim());
  }

  flushParagraph();
  closeList();
  if (inCode) {
    output.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`);
  }

  return { html: output.join('\n'), headings };
}

function renderToc(headings) {
  return headings
    .filter(item => item.level === 2 || item.level === 3)
    .map(item => `<a class="toc-level-${item.level}" href="#${item.slug}">${escapeHtml(item.label)}</a>`)
    .join('\n');
}

function renderPlainText(markdown) {
  const fencedBlocks = [];
  let text = markdown.replace(/```([^\n]*)\n([\s\S]*?)```/g, (_, language, code) => {
    const token = `@@FENCED_BLOCK_${fencedBlocks.length}@@`;
    fencedBlocks.push(`\n[CODE${language.trim() ? ` ${language.trim()}` : ''}]\n${code.trimEnd()}\n`);
    return token;
  });
  text = text
    .replace(/\r\n/g, '\n')
    .replace(/^!\[[^\]]*\]\([^\n]+\)\s*$/gm, '')
    .replace(/^#{1,6}\s+(.+)$/gm, (_, title) => `\n${title.toUpperCase()}\n${'='.repeat(Math.min(96, title.length))}`)
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '$1 ($2)')
    .replace(/\*\*([^*]+)\*\*/g, '$1')
    .replace(/\*([^*]+)\*/g, '$1')
    .replace(/`([^`]+)`/g, '$1')
    .replace(/^>\s?/gm, 'NOTE: ')
    .replace(/^---+\s*$/gm, '--------------------------------------------------------------------------------')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
  text = text.replace(/@@FENCED_BLOCK_(\d+)@@/g, (_, index) => fencedBlocks[Number(index)]);
  return text.replace(/\n{3,}/g, '\n\n').trim() + '\n';
}

const markdown = fs.readFileSync(markdownPath, 'utf8');
const firstDivider = markdown.indexOf('\n---\n');
const htmlMarkdown = firstDivider >= 0 ? markdown.slice(firstDivider + 5).trimStart() : markdown;
const { html: renderedBody, headings } = renderMarkdown(htmlMarkdown);
const toc = renderToc(headings);
const html = `<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="description" content="Cẩm nang kỹ thuật ProjectMgmt: nghiệp vụ Scrum, kiến trúc modular monolith, hai hệ AI, CSDL MySQL, frontend Angular và CI/CD.">
  <title>ProjectMgmt — Cẩm nang kỹ thuật hoàn thiện hệ thống</title>
  <style>
    :root { --ink:#101827; --muted:#526071; --paper:#f7f4ed; --panel:#ffffff; --navy:#061526; --cyan:#18b9d8; --gold:#d9a44a; --line:#d8d4ca; --danger:#b74235; --ok:#16724b; --shadow:0 24px 70px rgba(6,21,38,.13); }
    * { box-sizing:border-box; }
    html { scroll-behavior:smooth; max-width:100%; overflow-x:hidden; }
    body { margin:0; max-width:100%; overflow-x:hidden; color:var(--ink); background:var(--paper); font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif; line-height:1.72; }
    a { color:#076c83; text-underline-offset:3px; }
    .hero { min-height:82vh; display:grid; align-items:end; position:relative; overflow:hidden; color:white; background:var(--navy); }
    .hero::before { content:""; position:absolute; inset:0; background:linear-gradient(90deg,rgba(3,14,26,.92) 0%,rgba(3,14,26,.42) 48%,rgba(3,14,26,.08) 100%),linear-gradient(0deg,rgba(3,14,26,.92),transparent 65%),url("assets/projectmgmt-technical-handbook-cover.png") center/cover no-repeat; }
    .hero-inner { position:relative; width:min(1180px,calc(100% - 48px)); margin:0 auto; padding:84px 0 72px; }
    .eyebrow { color:#86e7f4; font-weight:800; letter-spacing:.16em; text-transform:uppercase; font-size:.78rem; }
    .hero h1 { max-width:850px; margin:.75rem 0 1rem; font-size:clamp(3rem,7vw,7.2rem); line-height:.93; letter-spacing:-.06em; }
    .hero p { max-width:760px; color:#dce8ef; font-size:clamp(1rem,2vw,1.32rem); }
    .hero-meta { display:flex; flex-wrap:wrap; gap:10px 28px; margin-top:28px; font-size:.9rem; color:#b9cbd8; }
    .layout { width:min(1380px,calc(100% - 36px)); margin:0 auto; display:grid; grid-template-columns:270px minmax(0,920px); justify-content:center; gap:54px; padding:64px 0 100px; }
    .toc { position:sticky; top:22px; align-self:start; max-height:calc(100vh - 44px); overflow:auto; padding:20px; border:1px solid var(--line); background:rgba(255,255,255,.83); backdrop-filter:blur(14px); box-shadow:0 12px 40px rgba(6,21,38,.07); }
    .toc strong { display:block; margin-bottom:12px; text-transform:uppercase; letter-spacing:.1em; font-size:.74rem; color:var(--muted); }
    .toc a { display:block; color:#253448; text-decoration:none; padding:5px 0; font-size:.84rem; line-height:1.35; }
    .toc a:hover { color:#077b95; }
    .toc-level-3 { padding-left:14px!important; color:#647386!important; }
    main { min-width:0; background:var(--panel); box-shadow:var(--shadow); padding:clamp(28px,6vw,78px); }
    main > h1:first-of-type { font-size:clamp(2.2rem,5vw,4.5rem); line-height:1; letter-spacing:-.045em; margin-top:0; }
    h2 { margin:3.8rem 0 1.25rem; padding-top:1rem; border-top:1px solid var(--line); font-size:clamp(1.75rem,4vw,2.8rem); line-height:1.1; letter-spacing:-.035em; }
    h3 { margin:2.5rem 0 .75rem; font-size:1.45rem; line-height:1.25; }
    h4 { margin:1.8rem 0 .5rem; font-size:1.08rem; text-transform:uppercase; letter-spacing:.04em; color:#394b5f; }
    p, li { max-width:78ch; }
    blockquote { margin:1.5rem 0; padding:18px 22px; border-left:4px solid var(--gold); background:#fff8e9; color:#39424e; }
    pre { max-width:100%; overflow:auto; padding:20px; color:#dcecff; background:#081827; border:1px solid #173149; border-radius:4px; font-size:.85rem; line-height:1.55; }
    code { padding:.12em .34em; background:#eef2f4; border-radius:3px; font-family:"Cascadia Code",Consolas,monospace; font-size:.9em; }
    pre code { padding:0; background:transparent; }
    .table-wrap { overflow-x:auto; margin:1.4rem 0 2rem; border:1px solid var(--line); }
    table { width:100%; border-collapse:collapse; font-size:.89rem; line-height:1.45; }
    th { padding:12px 14px; text-align:left; color:white; background:#10273b; vertical-align:top; }
    td { padding:11px 14px; border-top:1px solid #e4e1d9; vertical-align:top; }
    tbody tr:nth-child(even) { background:#faf9f5; }
    .diagram { margin:1.6rem 0 2.2rem; padding:18px; border:1px solid #b9ccd7; background:linear-gradient(145deg,#f7fbfc,#eef5f7); overflow:hidden; }
    .diagram figcaption { margin-bottom:13px; color:#3f5d6b; text-transform:uppercase; letter-spacing:.08em; font-size:.72rem; font-weight:900; }
    .diagram-row { display:grid; grid-template-columns:minmax(110px,1fr) 34px minmax(110px,1fr); gap:8px; align-items:center; position:relative; padding:9px 0; border-top:1px solid #d8e5ea; }
    .diagram-row:first-of-type { border-top:0; }
    .diagram .node { display:flex; align-items:center; min-height:42px; padding:8px 10px; color:#e9f6fa; background:#102c3f; border:1px solid #23536d; font-size:.82rem; font-weight:750; line-height:1.25; }
    .diagram .arrow { color:#0c809a; text-align:center; font-size:1.35rem; font-weight:900; }
    .diagram .edge-label { grid-column:1 / -1; justify-self:center; max-width:90%; padding:3px 9px; color:#6a4a0a; background:#fff2cf; border-radius:999px; font-size:.72rem; line-height:1.3; }
    .diagram .step { position:absolute; left:-10px; top:8px; width:22px; height:22px; display:grid; place-items:center; color:white; background:#d09530; border-radius:50%; font-size:.7rem; font-weight:900; }
    .diagram details { margin-top:12px; }
    .diagram summary { cursor:pointer; color:#526979; font-size:.78rem; font-weight:750; }
    .diagram details pre { margin-bottom:0; }
    img { max-width:100%; height:auto; }
    hr { border:0; border-top:1px solid var(--line); margin:3rem 0; }
    .evidence { display:inline-block; padding:.1rem .44rem; border-radius:999px; font-size:.7rem; line-height:1.4; font-weight:900; letter-spacing:.04em; vertical-align:.1em; }
    .verified { color:#075c3a; background:#dff4e8; }
    .inferred { color:#775200; background:#fff0bd; }
    .recommendation { color:#075b70; background:#d9f3f8; }
    footer { color:#d0dee8; background:#071725; padding:38px 24px; text-align:center; font-size:.86rem; }
    @media (max-width:980px) { .layout{display:block;width:min(calc(100% - 24px),920px);padding-top:24px}.toc{display:none}main{padding:28px}.hero{min-height:70vh}.hero-inner{width:min(calc(100% - 32px),900px)}.diagram-row{grid-template-columns:1fr 24px 1fr}.diagram .node{font-size:.72rem}.diagram{padding:12px} }
    @media (max-width:420px) { .diagram-row{grid-template-columns:1fr}.diagram .arrow{transform:rotate(90deg)}.diagram .edge-label{grid-column:1}.diagram .step{position:static;justify-self:center}.hero h1{font-size:2.7rem} }
    @media print { @page{size:A4;margin:16mm}.hero{min-height:0;break-after:page}.hero::before{background:var(--navy)}.hero-inner{padding:40mm 0}.layout{display:block;width:auto;padding:0}.toc{display:none}main{box-shadow:none;padding:0}h2{break-before:page}h3,table,pre{break-inside:avoid}a{color:inherit;text-decoration:none}footer{display:none} }
  </style>
</head>
<body>
  <header class="hero">
    <div class="hero-inner">
      <div class="eyebrow">Engineering design report · v1.0</div>
      <h1>ProjectMgmt</h1>
      <p>Cẩm nang kỹ thuật hoàn thiện hệ thống Quản lý Dự án Scrum + AI — từ nghiệp vụ, kiến trúc, dữ liệu và hai AI đến bảo mật, kiểm thử, CI/CD và phân công ba thành viên.</p>
      <div class="hero-meta"><span>Baseline: commit 05ce307</span><span>Ngày chốt: 20/09/2026</span><span>MySQL 55 bảng · 4 view · 2 trigger</span></div>
    </div>
  </header>
  <div class="layout">
    <nav class="toc" aria-label="Mục lục"><strong>Mục lục</strong>${toc}</nav>
    <main>${renderedBody}</main>
  </div>
  <footer>ProjectMgmt Technical Handbook · Nội dung sinh từ một nguồn Markdown duy nhất · Cập nhật 20/09/2026</footer>
</body>
</html>\n`;

fs.mkdirSync(handbookDir, { recursive: true });
fs.writeFileSync(htmlPath, html, 'utf8');
fs.writeFileSync(textPath, renderPlainText(markdown), 'utf8');
console.log(`Generated ${path.relative(repoRoot, htmlPath)}`);
console.log(`Generated ${path.relative(repoRoot, textPath)}`);
