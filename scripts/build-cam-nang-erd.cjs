/* Full, editable 55-table relationship atlas sourced from the canonical DDL.
 * sqlerd.py extracts physical FKs; this renderer lays out its graph without
 * Graphviz, which is not installed on the project workstation. */
const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const root = path.resolve(__dirname, '..');
const ddlPath = path.join(root, 'docs', 'projectmgmt_schema_mysql_optimized.sql');
const out = path.join(root, 'docs', 'cam-nang-du-an', 'diagrams');
const svgDir = path.join(out, 'svg');
const jsonPath = path.join(root, '.tmp', 'cam-nang-erd.json');
const py = 'C:\\Users\\hoang\\AppData\\Local\\Python\\bin\\python.exe';
const importer = 'C:\\Users\\hoang\\.agents\\skills\\drawio-skill\\scripts\\sqlerd.py';
const run = spawnSync(py, [importer, ddlPath, '--no-types', '-o', jsonPath], { encoding: 'utf8' });
if (run.status !== 0) throw new Error(run.stderr || run.stdout);
const graph = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
const ddl = fs.readFileSync(ddlPath, 'utf8');
const names = [...ddl.matchAll(/CREATE TABLE\s+`([^`]+)`\s*\(/g)].map(x => x[1]);
if (names.length !== 55 || graph.nodes.length !== 55 || graph.edges.length !== 52) throw new Error('Unexpected DDL/ERD count');
const groups = [
  { id: 'erd-a', name: 'ERD A — Danh tính, AI 1, Thông báo', title: 'A · IDENTITYEXPERIENCE · 16 BẢNG', from: ['User','UserProfile','ExternalLogin','OtpCode','RefreshToken','Role','Permission','RolePermission','UserRole','SkillCatalog','UserSkill','AiModel','AiPromptTemplate','AiGenerationLog','AiSuggestedTask','Notification'] },
  { id: 'erd-b', name: 'ERD B — Dự án, Scrum, AI 2', title: 'B · PLANNING · 18 BẢNG', from: ['Organization','Project','ProjectComponent','ProjectVersion','WorkflowStatus','WorkflowTransition','IssueType','Priority','Board','BoardColumn','Sprint','SprintSnapshot','SprintMemberCapacity','UserWorkloadSnapshot','UserPerformanceMetric','AiAssignmentRun','AiAssignmentCandidate','AiAssignmentDecision'] },
  { id: 'erd-c', name: 'ERD C — Issue và DataOps', title: 'C · DELIVERYINTELLIGENCE · 21 BẢNG', from: ['Issue','IssueLink','IssueWatcher','Comment','Attachment','ActivityLog','IssueStatusHistory','IssueAssignmentHistory','Label','IssueLabel','IssueComponentLink','IssueVersionLink','IssueRequiredSkill','AcceptanceCriteria','AiDataset','AiDatasetVersion','AiDatasetSample','AiDataCleaningRule','AiDataQualityFlag','AiTrainingRun','AiEvaluationResult'] }
];
const member = new Map(groups.flatMap(g => g.from.map(n => [n.toLowerCase(), g.id])));
if (member.size !== 55 || names.some(n => !member.has(n.toLowerCase()))) throw new Error('Group map differs from DDL');
const crossEdges = graph.edges.filter(e => member.get(e.source) !== member.get(e.target));
if (crossEdges.length !== 1 || crossEdges[0].source !== 'aievaluationresult' || crossEdges[0].target !== 'aimodel') throw new Error('Unexpected cross-module FK: '+JSON.stringify(crossEdges));
const xml = s => String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;').replaceAll('\n','&#xa;');
const clean = s => String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
const mono = s => String(s).replace(/#20272B|#4C5960|#69747A|#8B959A|#50585C|#30383D/gi, c => ({'#20272B':'#202020','#4C5960':'#555555','#69747A':'#777777','#8B959A':'#999999','#50585C':'#555555','#30383D':'#333333'})[c.toUpperCase()]);
const tableData = new Map();
for (const match of ddl.matchAll(/CREATE TABLE\s+`([^`]+)`\s*\(([\s\S]*?)\)\s*ENGINE=/g)) {
  const [, name, body] = match;
  const columns = [...body.matchAll(/^\s*`([^`]+)`\s+/gm)].map(x => x[1]);
  const pk = (body.match(/PRIMARY KEY\s*\(([^)]+)\)/i)?.[1].match(/`([^`]+)`/g) || []).map(x => x.slice(1,-1));
  const fk = [...body.matchAll(/FOREIGN KEY\s*\(`([^`]+)`\)/gi)].map(x => x[1]);
  const xmod = [...body.matchAll(/^\s*`([^`]+)`[^\n]*COMMENT\s+'XMOD[^\n]*/gmi)].map(x => x[1]);
  tableData.set(name.toLowerCase(), { name, columns, pk, fk, xmod });
}
if (tableData.size !== 55) throw new Error(`Parsed only ${tableData.size} CREATE TABLE blocks`);
const pages = [];
for (const g of groups) {
  const rows = Math.ceil(g.from.length / 4), W = 1690, H = 140 + rows * 285 + (g.id==='erd-c'?260:95);
  const nodes = g.from.map((name,i) => {
    const t = tableData.get(name.toLowerCase());
    const details = [t.pk.length ? `PK ${t.pk.join(', ')}` : 'PK —', ...t.fk.slice(0,3).map(x=>`FK ${x}`)];
    if (t.fk.length > 3) details.push(`+${t.fk.length-3} FK khác`);
    if (t.xmod.length) details.push(`XMOD ${t.xmod.slice(0,2).join(', ')}${t.xmod.length>2?` +${t.xmod.length-2}`:''}`);
    details.push(`${t.columns.length} cột · ${t.fk.length} FK vật lý`);
    return { id: name.toLowerCase(), name, x: 55+(i%4)*410, y: 145+Math.floor(i/4)*285, w: 355, h: 225, details };
  });
  const byId = new Map(nodes.map(n=>[n.id,n]));
  const edges = graph.edges.filter(e=>byId.has(e.source)&&byId.has(e.target));
  if (g.id === 'erd-c') {
    const ghost={id:'aimodel_external',name:'AiModel (module A)',x:670,y:H-255,w:355,h:145,details:['ModelId trong AiEvaluationResult','DDL còn FK vật lý chéo module','Quy tắc chuẩn: đổi thành XMOD + contract']};
    nodes.push(ghost);byId.set(ghost.id,ghost);
    edges.push({source:'aievaluationresult',target:ghost.id,label:'ModelId · FK chéo cần loại bỏ'});
  }
  const cells = ['<mxCell id="0"/>','<mxCell id="1" parent="0"/>'];
  for (const n of nodes) {
    const value = [n.name, ...n.details].join('\n');
    cells.push(`<mxCell id="${g.id}_${n.id}" value="${xml(value)}" style="swimlane;html=1;whiteSpace=wrap;rounded=0;startSize=34;horizontal=1;fillColor=#FFFFFF;swimlaneFillColor=#FFFFFF;strokeColor=#555555;fontColor=#202020;fontFamily=Arial;fontSize=16;align=left;spacingLeft=12;verticalAlign=top;" vertex="1" parent="1"><mxGeometry x="${n.x}" y="${n.y}" width="${n.w}" height="${n.h}" as="geometry"/></mxCell>`);
  }
  edges.forEach((e,i)=>cells.push(`<mxCell id="${g.id}_fk_${i}" value="${xml(e.label||'')}" style="edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#50585C;strokeWidth=1.5;fontColor=#30383D;fontSize=12;labelBackgroundColor=#FFFFFF;startArrow=ERmany;startFill=0;endArrow=ERone;endFill=0;" edge="1" parent="1" source="${g.id}_${e.source}" target="${g.id}_${e.target}"><mxGeometry relative="1" as="geometry"/></mxCell>`));
  pages.push(`<diagram id="${g.id}" name="${xml(g.name)}"><mxGraphModel dx="1600" dy="1200" grid="1" gridSize="10" page="1" pageScale="1" pageWidth="${W}" pageHeight="${H}" background="#FFFFFF"><root>${cells.join('')}</root></mxGraphModel></diagram>`);
  const svgEdges = edges.map(e => { const a=byId.get(e.source),b=byId.get(e.target);const x1=a.x+a.w/2,y1=a.y+a.h/2,x2=b.x+b.w/2,y2=b.y+b.h/2;return `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="#8B959A" stroke-width="1.5" marker-end="url(#arrow)"/>`; }).join('');
  const svgNodes = nodes.map(n=>`<g><rect x="${n.x}" y="${n.y}" width="${n.w}" height="${n.h}" fill="#FFFFFF" stroke="#555555" stroke-width="2"/><rect x="${n.x}" y="${n.y}" width="${n.w}" height="36" fill="#EEEEEE" stroke="#555555" stroke-width="2"/><text x="${n.x+12}" y="${n.y+25}" font-family="Arial" font-size="21" font-weight="700" fill="#202020">${clean(n.name)}</text>${n.details.map((s,i)=>`<text x="${n.x+12}" y="${n.y+61+i*25}" font-family="Arial" font-size="16" fill="#202020">${clean(s)}</text>`).join('')}</g>`).join('');
  const subtitle=g.id==='erd-c'?'Quan hệ vật lý trong module; một FK chéo còn trong DDL được nêu rõ để sửa thành XMOD.':'Quan hệ vật lý trong module; XMOD là GUID logic, không có khóa ngoại liên module.';
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}"><rect width="100%" height="100%" fill="#FFFFFF"/><defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M0 0 L10 5 L0 10z" fill="#8B959A"/></marker></defs><text x="55" y="57" font-family="Arial" font-size="28" font-weight="700" fill="#20272B">${clean(g.title)}</text><text x="55" y="91" font-family="Arial" font-size="17" fill="#4C5960">${clean(subtitle)}</text>${svgEdges}${svgNodes}<text x="55" y="${H-33}" font-family="Arial" font-size="15" fill="#69747A">Nguồn: projectmgmt_schema_mysql_optimized.sql · Bản chỉnh sửa: ERD-55-bang.drawio</text></svg>`;
  fs.writeFileSync(path.join(svgDir, `${g.id}.svg`), mono(svg), 'utf8');
  console.log(`${g.name}: ${g.from.length} tables, ${edges.length} shown relationships${g.id==='erd-c'?' (one cross-module FK shown as an exception)':''}`);
}
const master=`<?xml version="1.0" encoding="UTF-8"?><mxfile host="app.diagrams.net" agent="Cam nang ky thuat" version="24.7.17" type="device">${pages.join('')}</mxfile>`;
fs.writeFileSync(path.join(out, 'ERD-55-bang.drawio'), mono(master), 'utf8');
console.log(`Generated 3-page, 55-table ERD and SVG in ${out}`);
