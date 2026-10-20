// Isolated API :5084 and frontend :4200. Requires WORKSPACE_TEST_EMAIL/PASSWORD with role-management permission.
const puppeteer = require('puppeteer');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
(async () => {
  const results = [];
  async function api(method, url, body, token) {
    const r = await fetch('http://localhost:5084' + url, { method,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer '+token } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body) });
    assert(r.ok, method+' '+url+': '+r.status);
    return r.json();
  }
  const session = await api('POST', '/api/v1/auth/login', { email: process.env.WORKSPACE_TEST_EMAIL, password: process.env.WORKSPACE_TEST_PASSWORD });
  const tag = Date.now().toString(36);
  const org = await api('POST', '/api/v1/organizations', { name: 'Browser customization '+tag, slug: 'bc-'+tag }, session.accessToken);
  const project = await api('POST', '/api/v1/projects', { orgId: org.id, name: 'Browser customization', projectKey: 'B'+tag.slice(-7).toUpperCase() }, session.accessToken);
  const roleName = 'Browser'+tag;
  const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'] });
  try {
    const page = await browser.newPage();
    await page.setViewport({ width: 1500, height: 1050 });
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.setRequestInterception(true);
    page.on('request', r => r.continue({ url: r.url().replace('http://localhost:5083','http://localhost:5084') }));
    await page.evaluateOnNewDocument((s,p) => {
      sessionStorage.setItem('projectmgmt.auth-session', JSON.stringify(s));
      sessionStorage.setItem('projectmgmt.selected-project', p);
    }, session, project.id);
    async function clickText(selector, text) {
      assert(await page.evaluate((selector,text) => {
        const e = [...document.querySelectorAll(selector)].find(e => e.textContent.includes(text));
        if (e) e.click(); return !!e;
      }, selector, text), 'Missing control: '+text);
    }
    async function adminReady() {
      await page.waitForFunction(() => {
        const e=document.querySelector('app-rbac-admin-page');
        return e && !ng.getComponent(e).loading() && ng.getComponent(e).roles().length > 0;
      });
    }
    await page.goto('http://localhost:4200/admin/identity', { waitUntil:'networkidle2' });
    await adminReady();
    await clickText('button', 'Danh Sách Người Dùng');
    await page.waitForSelector('.users-table tbody .user-cell');
    results.push('Admin directory loads actual scoped accounts');
    await clickText('button', 'Ma Trận Phân Quyền');
    await clickText('button', 'Tạo Vai Trò Mới');
    await page.type('#newRoleName', roleName);
    await page.type('#newRoleDesc', 'Browser initial permissions');
    await page.evaluate(() => {
      [...document.querySelectorAll('.perm-checkbox-item')].find(e => e.textContent.includes('project.read')).querySelector('input').click();
    });
    await page.click('.btn-submit');
    await page.waitForFunction(() => !document.querySelector('.modal-backdrop'));
    await page.reload({waitUntil:'networkidle2'}); await adminReady();
    const role = (await api('GET','/api/v1/roles',undefined,session.accessToken)).items.find(r => r.name===roleName);
    assert(role.permissionIds.length===1);
    const label = 'Permission project.read for role '+roleName;
    assert(await page.$eval('input[aria-label="'+label+'"]', e=>e.checked));
    results.push('Create role with initial permissions survives reload');
    await page.click('input[aria-label="'+label+'"]');
    const write = page.waitForResponse(r=>r.url().includes('/roles/'+role.roleId+'/permissions') && r.request().method()==='PUT');
    await page.click('.btn-save-matrix');
    assert((await write).status()===200);
    await page.reload({waitUntil:'networkidle2'}); await adminReady();
    assert.equal(await page.$eval('input[aria-label="'+label+'"]', e=>e.checked), false);
    results.push('Matrix permission removal persists after reload');

    await page.goto('http://localhost:4200/projects/'+project.id+'/settings/workflow', { waitUntil:'networkidle2' });
    await page.waitForSelector('#workflowTemplate');
    await page.select('#workflowTemplate', 'agile-qa');
    const applied = page.waitForResponse(r=>r.url().includes('/workflow/template') && r.request().method()==='POST');
    await clickText('button','Áp dụng mẫu');
    assert((await applied).status()===200);
    await page.reload({waitUntil:'networkidle2'});
    await page.waitForFunction(()=>[...document.querySelectorAll('.status-badge-item')].some(e=>e.textContent.includes('QA Testing')));
    results.push('Workflow template survives reload');
    await page.goto('http://localhost:4200/backlog',{waitUntil:'networkidle2'});
    await page.waitForFunction(() => {
      const e=document.querySelector('app-backlog-page');
      return e && !ng.getComponent(e).projectService.workspaceLoading();
    });
    await clickText('.app-topbar button','Tạo việc mới');
    await page.waitForSelector('#new-task-title');
    await clickText('app-create-task-modal button','Tạo Loại Mới');
    const typeName='Risk '+tag;
    await page.type('input[aria-label="Tên loại công việc mới"]',typeName);
    const created = page.waitForResponse(r=>r.url().includes('/issue-types') && r.request().method()==='POST');
    await page.evaluate(()=> {
      [...document.querySelectorAll('app-create-task-modal button')].find(e=>e.textContent.trim()==='Tạo').click();
    });
    assert((await created).status()===201);
    await page.waitForSelector('#new-task-type');
    assert.equal(await page.$eval('#new-task-type',e=>e.value),typeName);
    await page.reload({waitUntil:'networkidle2'});
    await clickText('.app-topbar button','Tạo việc mới');
    await page.waitForSelector('#new-task-type');
    await page.waitForFunction(name=>[...document.querySelector('#new-task-type').options].some(e=>e.value===name),{},typeName);
    results.push('Custom issue type is available after reload');
    assert.deepEqual(errors,[]);
    const out=path.join(os.tmpdir(),'projectmgmt-audit','customization-browser.json');
    fs.mkdirSync(path.dirname(out),{recursive:true});
    fs.writeFileSync(out,JSON.stringify({results,pageErrors:errors},null,2));
    console.log('PASS: '+results.length+' browser flows');
  } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exitCode=1;});
