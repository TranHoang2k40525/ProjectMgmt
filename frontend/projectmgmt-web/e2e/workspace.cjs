// Run against the isolated API (:5084), with WORKSPACE_TEST_EMAIL/PASSWORD and WORKSPACE_OTHER_EMAIL.
const puppeteer = require('puppeteer');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

(async () => {
  const results = [];
  async function api(method, url, body, token) {
    const response = await fetch('http://localhost:5084' + url, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
    assert(response.ok, `${method} ${url}: ${response.status}`); return response.json();
  }
  const session = await api('POST', '/api/v1/auth/login', { email: process.env.WORKSPACE_TEST_EMAIL, password: process.env.WORKSPACE_TEST_PASSWORD });
  const suffix = Date.now().toString(36);
  const org = await api('POST', '/api/v1/organizations', { name: `Browser test ${suffix}`, slug: `browser-${suffix}` }, session.accessToken);
  const project = await api('POST', '/api/v1/projects', { orgId: org.id, name: 'Browser persistence test', projectKey: `B${suffix.slice(-7).toUpperCase()}` }, session.accessToken);
  const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'] });
  try {
    const page = await browser.newPage(); await page.setViewport({ width: 1500, height: 1050 });
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.setRequestInterception(true);
    page.on('request', request => request.continue({ url: request.url().replace('http://localhost:5083', 'http://localhost:5084') }));
    await page.evaluateOnNewDocument((s, p) => {
      sessionStorage.setItem('projectmgmt.auth-session', JSON.stringify(s));
      sessionStorage.setItem('projectmgmt.selected-project', p);
    }, session, project.id);
    async function clickText(selector, text) {
      const found = await page.evaluate((s, t) => {
        const element = [...document.querySelectorAll(s)].find(e => e.textContent.includes(t));
        if (element) element.click(); return !!element;
      }, selector, text);
      assert(found, `Missing ${selector}: ${text}`);
    }
    async function idle(selector = 'app-backlog-page') {
      await page.waitForFunction(s => {
        const element = document.querySelector(s); if (!element || !window.ng) return false;
        const service = ng.getComponent(element).projectService;
        return service && !service.workspaceLoading() && !service.pendingSaves();
      }, { timeout: 20000 }, selector);
    }
    await page.goto('http://localhost:4200/backlog', { waitUntil: 'networkidle2' }); await idle();
    await clickText('button', 'Tạo Sprint');
    await page.waitForFunction(() => ng.getComponent(document.querySelector('app-backlog-page')).projectService.sprints().length === 1);
    await idle(); results.push('create sprint through UI');
    await clickText('.app-topbar button', 'Tạo việc mới');
    await page.waitForSelector('#new-task-title'); await page.type('#new-task-title', 'Browser persistent task');
    await clickText('app-create-task-modal button', 'Tạo công việc');
    await page.waitForFunction(() => ng.getComponent(document.querySelector('app-backlog-page')).projectService.workItems().length === 1);
    await idle();
    const task = await page.evaluate(() => ng.getComponent(document.querySelector('app-backlog-page')).projectService.workItems()[0]);
    assert(task.issueKey.startsWith(project.projectKey));
    await page.reload({ waitUntil: 'networkidle2' }); await idle();
    const restored = await page.evaluate(() => { const s = ng.getComponent(document.querySelector('app-backlog-page')).projectService; return { items: s.workItems(), sprints: s.sprints(), error: s.saveError() }; });
    assert.equal(restored.items[0].id, task.id); assert.equal(restored.sprints.length, 1); assert.equal(restored.error, null);
    results.push('task and sprint survive browser reload');
    await page.goto('http://localhost:4200/board', { waitUntil: 'networkidle2' }); await idle('app-board-page');
    const card = await page.$('.board-column:nth-child(1) .cdk-drag');
    const destination = await page.$('.board-column:nth-child(2)');
    assert(card && destination, 'Missing draggable board elements');
    const a = await card.boundingBox(), b = await destination.boundingBox();
    await page.mouse.move(a.x + a.width / 2, a.y + a.height / 2); await page.mouse.down();
    await page.mouse.move(a.x + a.width / 2 + 12, a.y + a.height / 2 + 12, { steps: 5 });
    await page.mouse.move(b.x + b.width / 2, b.y + 110, { steps: 30 });
    await new Promise(resolve => setTimeout(resolve, 400)); await page.mouse.up();
    await page.waitForFunction(() => ng.getComponent(document.querySelector('app-board-page')).projectService.workItems()[0]?.statusName === 'In Progress', { timeout: 12000 });
    await idle('app-board-page');
    await page.reload({ waitUntil: 'networkidle2' }); await idle('app-board-page');
    assert.equal((await api('GET', `/api/v1/issues/${task.id}`, undefined, session.accessToken)).statusName, 'In Progress');
    results.push('real mouse drag between Kanban columns persists');
    await page.goto(`http://localhost:4200/issues/${task.id}`, { waitUntil: 'networkidle2' });
    await page.waitForSelector('textarea[aria-label="Bình luận mới"]');
    await page.type('textarea[aria-label="Bình luận mới"]', 'Browser saved comment'); await clickText('button', 'Gửi bình luận');
    await page.waitForFunction(() => document.body.innerText.includes('Browser saved comment') && document.querySelectorAll('article').length > 0);
    await page.type('input[aria-label="Tiêu chí mới"]', 'Browser acceptance criterion'); await clickText('button', 'Thêm tiêu chí');
    await page.waitForFunction(() => document.querySelector('label.row')?.textContent.includes('Browser acceptance criterion'));
    const fixture = path.join(os.tmpdir(), 'projectmgmt-browser-attachment.txt'); fs.writeFileSync(fixture, 'browser attachment content');
    await (await page.$('input[type=file]')).uploadFile(fixture);
    await page.waitForFunction(() => document.body.innerText.includes('projectmgmt-browser-attachment.txt'));
    await page.reload({ waitUntil: 'networkidle2' });
    await page.waitForFunction(() => document.body.innerText.includes('Browser saved comment') && document.body.innerText.includes('projectmgmt-browser-attachment.txt'));
    results.push('issue detail, comment, criterion and attachment survive reload');
    await page.goto('http://localhost:4200/project/members', { waitUntil: 'networkidle2' });
    await clickText('.members-header button', 'Thêm thành viên');
    await page.waitForSelector('#member-email'); await page.type('#member-name', 'Other member'); await page.type('#member-email', process.env.WORKSPACE_OTHER_EMAIL);
    await clickText('.members-modal button', 'Thêm thành viên');
    await page.waitForFunction(email => [...document.querySelectorAll('.members-grid span')].some(e => e.textContent === email), { timeout: 15000 }, process.env.WORKSPACE_OTHER_EMAIL);
    results.push('invite existing user through member form');
    await page.goto('http://localhost:4200/profile', { waitUntil: 'networkidle2' });
    await page.waitForFunction(() => ng.getComponent(document.querySelector('app-profile-page')).activeSessions().some(s => s.isCurrent));
    results.push('profile lists real sessions including current device');
    assert.deepEqual(errors, []);
    const out = path.join(os.tmpdir(), 'projectmgmt-audit', 'workspace-browser-results.json');
    fs.writeFileSync(out, JSON.stringify({ results, errors, projectId: project.id }, null, 2));
    console.log(JSON.stringify(results, null, 2)); console.log(`PASS ${results.length} browser flows; ${out}`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
